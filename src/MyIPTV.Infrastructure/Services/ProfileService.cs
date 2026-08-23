using Microsoft.Extensions.Logging;
using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;

namespace MyIPTV.Infrastructure.Services;

public sealed partial class ProfileService(
    IProfileRepository repository,
    ICredentialService credentialService,
    IProfileValidator validator,
    IProfileConnectionTester connectionTester,
    IEnumerable<IContentProvider> contentProviders,
    IChannelCatalog channelCatalog,
    IMediaCatalog mediaCatalog,
    IFavoriteRepository favoriteRepository,
    IWatchHistoryRepository watchHistoryRepository,
    IActiveProfileService activeProfileService,
    ILogger<ProfileService> logger) : IProfileService
{
    private readonly ILogger<ProfileService> _logger = logger;

    public Task<IReadOnlyList<IptvProfile>> GetAllAsync(CancellationToken cancellationToken = default) =>
        repository.GetAllAsync(cancellationToken);

    public async Task<ProfileDraft?> GetDraftAsync(
        Guid profileId,
        CancellationToken cancellationToken = default)
    {
        IptvProfile? profile = await repository.GetByIdAsync(profileId, cancellationToken);
        if (profile is null)
        {
            return null;
        }

        ProfileCredentials? credentials = profile.ConnectionType == ProfileConnectionType.StalkerPortal
            ? null
            : await credentialService.RetrieveAsync(profileId, cancellationToken);
        string address = profile.ConnectionType == ProfileConnectionType.M3uPlaylist && credentials is not null
            ? M3uCredentialUrl.Add(profile.ServerAddress, credentials)
            : profile.ServerAddress;
        return new ProfileDraft
        {
            Id = profile.Id,
            Name = profile.Name,
            ConnectionType = profile.ConnectionType,
            ServerAddress = address,
            Username = credentials?.Username ?? profile.Username,
            Password = credentials?.Password,
        };
    }

    public async Task<ProfileSaveResult> SaveAsync(
        ProfileDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        IptvProfile? existing = draft.Id.HasValue
            ? await repository.GetByIdAsync(draft.Id.Value, cancellationToken)
            : null;
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

        IReadOnlyList<IptvProfile> profiles = await repository.GetAllAsync(cancellationToken);
        if (profiles.Any(profile =>
                profile.Id != draft.Id &&
                string.Equals(profile.Name.Trim(), draft.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return ProfileSaveResult.Failure("A profile with this name already exists.");
        }

        M3uAddressParts m3uParts = draft.ConnectionType == ProfileConnectionType.M3uPlaylist
            ? M3uCredentialUrl.Split(draft.ServerAddress.Trim())
            : new M3uAddressParts(draft.ServerAddress.Trim(), null, false);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        IptvProfile profile = new(
            draft.Id ?? Guid.NewGuid(),
            draft.Name.Trim(),
            draft.ConnectionType,
            m3uParts.SanitizedAddress,
            draft.ConnectionType == ProfileConnectionType.M3uPlaylist ? null : NullIfWhiteSpace(draft.Username),
            existing?.CreatedUtc ?? now,
            now);

        await repository.UpsertAsync(profile, cancellationToken);

        if (profile.ConnectionType != ProfileConnectionType.M3uPlaylist)
        {
            channelCatalog.RemoveProfile(profile.Id);
        }
        else
        {
            mediaCatalog.RemoveProfile(profile.Id);
        }

        if (draft.ConnectionType == ProfileConnectionType.M3uPlaylist)
        {
            ProfileCredentials? suppliedCredentials = m3uParts.Credentials ??
                (!string.IsNullOrEmpty(draft.Password)
                    ? new ProfileCredentials(NullIfWhiteSpace(draft.Username), draft.Password)
                    : null);
            if (suppliedCredentials is not null)
            {
                await credentialService.StoreAsync(profile.Id, suppliedCredentials, cancellationToken);
            }
            else if (existing?.ConnectionType != ProfileConnectionType.M3uPlaylist)
            {
                await credentialService.DeleteAsync(profile.Id, cancellationToken);
            }
        }
        else if (draft.ConnectionType == ProfileConnectionType.StalkerPortal)
        {
            // Stalker MAC authentication is stored in the profile itself. Remove any
            // credential left behind when this profile previously used Xtream or M3U.
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
        channelCatalog.RemoveProfile(profileId);
        mediaCatalog.RemoveProfile(profileId);
        await favoriteRepository.RemoveForProfileAsync(profileId, cancellationToken);
        await watchHistoryRepository.RemoveForProfileAsync(profileId, cancellationToken);
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

        IContentProvider? provider = contentProviders.FirstOrDefault(
            item => item.ConnectionType == saved.Profile.ConnectionType);
        if (provider is not null)
        {
            IptvProfile effectiveProfile = await WithStoredCredentialsAsync(saved.Profile, cancellationToken);
            ProviderLoadResult loaded = await provider.LoadCatalogAsync(
                effectiveProfile,
                cancellationToken);
            if (!loaded.IsSuccess)
            {
                return ConnectionTestResult.Failure(loaded.Message);
            }

            activeProfileService.SetActive(saved.Profile);
            return ConnectionTestResult.Success(loaded.Message);
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
        if (draft.ConnectionType == ProfileConnectionType.StalkerPortal)
        {
            return draft;
        }

        M3uAddressParts suppliedM3u = draft.ConnectionType == ProfileConnectionType.M3uPlaylist
            ? M3uCredentialUrl.Split(draft.ServerAddress)
            : new M3uAddressParts(draft.ServerAddress, null, false);
        if (!string.IsNullOrEmpty(draft.Password) || suppliedM3u.Credentials is not null || !draft.Id.HasValue)
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
                ServerAddress = draft.ConnectionType == ProfileConnectionType.M3uPlaylist
                    ? M3uCredentialUrl.Add(draft.ServerAddress, credentials)
                    : draft.ServerAddress,
                Username = credentials.Username ?? draft.Username,
                Password = credentials.Password,
            };
    }

    private async Task<IptvProfile> WithStoredCredentialsAsync(
        IptvProfile profile,
        CancellationToken cancellationToken)
    {
        if (profile.ConnectionType != ProfileConnectionType.M3uPlaylist)
        {
            return profile;
        }

        ProfileCredentials? credentials = await credentialService.RetrieveAsync(profile.Id, cancellationToken);
        return credentials is null
            ? profile
            : profile with { ServerAddress = M3uCredentialUrl.Add(profile.ServerAddress, credentials) };
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
