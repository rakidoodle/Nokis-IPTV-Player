using Microsoft.Extensions.Logging;
using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;

namespace MyIPTV.Infrastructure.Services;

public sealed partial class ProfileService(
    IProfileRepository repository,
    ICredentialService credentialService,
    IProfileValidator validator,
    IProfileConnectionTester connectionTester,
    IActiveProfileService activeProfileService,
    ILogger<ProfileService> logger) : IProfileService
{
    private readonly ILogger<ProfileService> _logger = logger;

    public Task<IReadOnlyList<IptvProfile>> GetAllAsync(CancellationToken cancellationToken = default) =>
        repository.GetAllAsync(cancellationToken);

    public async Task<ProfileSaveResult> SaveAsync(
        ProfileDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ProfileCredentials? existingCredentials = draft.Id.HasValue
            ? await credentialService.RetrieveAsync(draft.Id.Value, cancellationToken)
            : null;
        bool requiresPassword = draft.ConnectionType == ProfileConnectionType.XtreamApi &&
                                existingCredentials is null &&
                                string.IsNullOrEmpty(draft.Password);
        ProfileValidationResult validation = validator.Validate(draft, requiresPassword);
        if (!validation.IsValid)
        {
            return ProfileSaveResult.Failure(validation.Message);
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        IptvProfile? existing = draft.Id.HasValue
            ? await repository.GetByIdAsync(draft.Id.Value, cancellationToken)
            : null;
        IptvProfile profile = new(
            draft.Id ?? Guid.NewGuid(),
            draft.Name.Trim(),
            draft.ConnectionType,
            draft.ServerAddress.Trim(),
            NullIfWhiteSpace(draft.Username),
            existing?.CreatedUtc ?? now,
            now);

        await repository.UpsertAsync(profile, cancellationToken);

        if (draft.ConnectionType == ProfileConnectionType.M3uPlaylist)
        {
            await credentialService.DeleteAsync(profile.Id, cancellationToken);
        }
        else if (!string.IsNullOrEmpty(draft.Password))
        {
            await credentialService.StoreAsync(
                profile.Id,
                new ProfileCredentials(profile.Username, draft.Password),
                cancellationToken);
        }
        else if (existingCredentials is not null && existingCredentials.Username != profile.Username)
        {
            await credentialService.StoreAsync(
                profile.Id,
                new ProfileCredentials(profile.Username, existingCredentials.Password),
                cancellationToken);
        }

        LogProfileSaved(profile.Id, profile.ConnectionType);
        return ProfileSaveResult.Success(profile);
    }

    public async Task DeleteAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        await repository.DeleteAsync(profileId, cancellationToken);
        await credentialService.DeleteAsync(profileId, cancellationToken);
        activeProfileService.Clear(profileId);
        LogProfileDeleted(profileId);
    }

    public async Task<ConnectionTestResult> TestConnectionAsync(
        ProfileDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ProfileDraft effectiveDraft = await WithStoredCredentialsAsync(draft, cancellationToken);
        ProfileValidationResult validation = validator.Validate(
            effectiveDraft,
            requireCredentials: effectiveDraft.ConnectionType == ProfileConnectionType.XtreamApi);
        return validation.IsValid
            ? await connectionTester.TestAsync(effectiveDraft, cancellationToken)
            : ConnectionTestResult.Failure(validation.Message);
    }

    public async Task<ConnectionTestResult> ConnectAsync(
        ProfileDraft draft,
        CancellationToken cancellationToken = default)
    {
        ProfileSaveResult saved = await SaveAsync(draft, cancellationToken);
        if (!saved.IsSuccess || saved.Profile is null)
        {
            return ConnectionTestResult.Failure(saved.Message);
        }

        ProfileDraft savedDraft = new()
        {
            Id = saved.Profile.Id,
            Name = saved.Profile.Name,
            ConnectionType = saved.Profile.ConnectionType,
            ServerAddress = saved.Profile.ServerAddress,
            Username = saved.Profile.Username,
            Password = draft.Password,
        };
        ConnectionTestResult tested = await TestConnectionAsync(savedDraft, cancellationToken);
        if (!tested.IsSuccess)
        {
            return tested;
        }

        activeProfileService.SetActive(saved.Profile);
        return ConnectionTestResult.Success(
            "Profile connected. Content loading will become available with its provider module.");
    }

    private async Task<ProfileDraft> WithStoredCredentialsAsync(
        ProfileDraft draft,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(draft.Password) || !draft.Id.HasValue)
        {
            return draft;
        }

        ProfileCredentials? credentials = await credentialService.RetrieveAsync(
            draft.Id.Value,
            cancellationToken);
        return credentials is null
            ? draft
            : new ProfileDraft
            {
                Id = draft.Id,
                Name = draft.Name,
                ConnectionType = draft.ConnectionType,
                ServerAddress = draft.ServerAddress,
                Username = draft.Username,
                Password = credentials.Password,
            };
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [LoggerMessage(
        EventId = 3101,
        Level = LogLevel.Information,
        Message = "Saved profile {ProfileId} of type {ConnectionType}.")]
    private partial void LogProfileSaved(Guid profileId, ProfileConnectionType connectionType);

    [LoggerMessage(EventId = 3102, Level = LogLevel.Information, Message = "Deleted profile {ProfileId}.")]
    private partial void LogProfileDeleted(Guid profileId);
}
