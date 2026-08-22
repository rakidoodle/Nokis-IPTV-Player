using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;

namespace MyIPTV.Infrastructure.Security;

public sealed partial class WindowsCredentialService(
    IApplicationPaths paths,
    ILogger<WindowsCredentialService> logger) : ICredentialService, IDisposable
{
    private const string FileExtension = ".credential";
    private static readonly byte[] OptionalEntropy = Encoding.UTF8.GetBytes("MyIPTV.Credentials.v1");
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.General);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ILogger<WindowsCredentialService> _logger = logger;

    public async Task StoreAsync(
        Guid profileId,
        ProfileCredentials credentials,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        byte[] plaintext = JsonSerializer.SerializeToUtf8Bytes(
            new CredentialPayload(credentials.Username, credentials.Password),
            SerializerOptions);
        byte[]? protectedBytes = null;
        bool lockTaken = false;

        try
        {
            await _gate.WaitAsync(cancellationToken);
            lockTaken = true;
            paths.EnsureDirectoriesExist();
            protectedBytes = ProtectedData.Protect(
                plaintext,
                OptionalEntropy,
                DataProtectionScope.CurrentUser);

            string destinationPath = GetCredentialPath(profileId);
            string temporaryPath = destinationPath + ".tmp";
            try
            {
                await File.WriteAllBytesAsync(temporaryPath, protectedBytes, cancellationToken);
                File.Move(temporaryPath, destinationPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }

            LogCredentialStored(profileId);
        }
        finally
        {
            if (lockTaken)
            {
                _gate.Release();
            }

            CryptographicOperations.ZeroMemory(plaintext);
            if (protectedBytes is not null)
            {
                CryptographicOperations.ZeroMemory(protectedBytes);
            }
        }
    }

    public async Task<ProfileCredentials?> RetrieveAsync(
        Guid profileId,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        byte[]? protectedBytes = null;
        byte[]? plaintext = null;
        try
        {
            string credentialPath = GetCredentialPath(profileId);
            if (!File.Exists(credentialPath))
            {
                return null;
            }

            protectedBytes = await File.ReadAllBytesAsync(credentialPath, cancellationToken);
            plaintext = ProtectedData.Unprotect(
                protectedBytes,
                OptionalEntropy,
                DataProtectionScope.CurrentUser);
            CredentialPayload? payload = JsonSerializer.Deserialize<CredentialPayload>(
                plaintext,
                SerializerOptions);

            if (payload is null || string.IsNullOrEmpty(payload.Password))
            {
                LogCredentialUnreadable(profileId, null);
                return null;
            }

            return new ProfileCredentials(payload.Username, payload.Password);
        }
        catch (CryptographicException exception)
        {
            LogCredentialUnreadable(profileId, exception);
            return null;
        }
        catch (JsonException exception)
        {
            LogCredentialUnreadable(profileId, exception);
            return null;
        }
        catch (IOException exception)
        {
            LogCredentialUnreadable(profileId, exception);
            return null;
        }
        finally
        {
            if (plaintext is not null)
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }

            if (protectedBytes is not null)
            {
                CryptographicOperations.ZeroMemory(protectedBytes);
            }

            _gate.Release();
        }
    }

    public async Task DeleteAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            string credentialPath = GetCredentialPath(profileId);
            if (File.Exists(credentialPath))
            {
                File.Delete(credentialPath);
                LogCredentialDeleted(profileId);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private string GetCredentialPath(Guid profileId) =>
        Path.Combine(paths.CredentialsDirectory, profileId.ToString("N") + FileExtension);

    public void Dispose() => _gate.Dispose();

    [LoggerMessage(
        EventId = 3201,
        Level = LogLevel.Debug,
        Message = "Stored Windows-protected credentials for profile {ProfileId}.")]
    private partial void LogCredentialStored(Guid profileId);

    [LoggerMessage(
        EventId = 3202,
        Level = LogLevel.Warning,
        Message = "Windows-protected credentials for profile {ProfileId} could not be read.")]
    private partial void LogCredentialUnreadable(Guid profileId, Exception? exception);

    [LoggerMessage(
        EventId = 3203,
        Level = LogLevel.Debug,
        Message = "Deleted Windows-protected credentials for profile {ProfileId}.")]
    private partial void LogCredentialDeleted(Guid profileId);

    private sealed record CredentialPayload(string? Username, string Password);
}
