using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MyIPTV.App.Services;
using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;

namespace MyIPTV.App.ViewModels;

public partial class ProfilesViewModel : ObservableObject
{
    private readonly IPlaylistFilePicker _filePicker;
    private readonly ILogger<ProfilesViewModel> _logger;
    private readonly IUserNotificationService _notifications;
    private readonly IProfileService _profileService;

    [ObservableProperty]
    private IptvProfile? _selectedProfile;

    [ObservableProperty]
    private string _profileName = string.Empty;

    [ObservableProperty]
    private ProfileTypeOption _selectedProfileType;

    [ObservableProperty]
    private string _serverAddress = string.Empty;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isImporting;

    [ObservableProperty]
    private bool _isLoaded;

    [ObservableProperty]
    private string _statusMessage = "Create a profile or select an existing one.";

    [ObservableProperty]
    private bool _isStatusError;

    public ProfilesViewModel(
        IProfileService profileService,
        IUserNotificationService notifications,
        IPlaylistFilePicker filePicker,
        ILogger<ProfilesViewModel> logger)
    {
        _profileService = profileService;
        _notifications = notifications;
        _filePicker = filePicker;
        _logger = logger;
        ProfileTypes =
        [
            new("M3U Playlist", ProfileConnectionType.M3uPlaylist),
            new("Xtream API", ProfileConnectionType.XtreamApi),
            new("Stalker / Ministra Portal", ProfileConnectionType.StalkerPortal),
        ];
        _selectedProfileType = ProfileTypes[0];
    }

    public ObservableCollection<IptvProfile> Profiles { get; } = [];

    public IReadOnlyList<ProfileTypeOption> ProfileTypes { get; }

    public bool HasProfiles => Profiles.Count > 0;

    public bool HasSelectedProfile => SelectedProfile is not null;

    public bool ShowsCredentialFields =>
        SelectedProfileType.Value != ProfileConnectionType.M3uPlaylist;

    public bool ShowsBrowseButton =>
        SelectedProfileType.Value == ProfileConnectionType.M3uPlaylist;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsLoaded || IsBusy)
        {
            return;
        }

        await ReloadAsync(profileIdToSelect: null);
        IsLoaded = true;
    }

    [RelayCommand]
    private void AddNew()
    {
        SelectedProfile = null;
        ClearEditor();
        SetStatus("Enter the details for your new profile.", isError: false);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await RunBusyAsync(async () =>
        {
            ProfileSaveResult result = await _profileService.SaveAsync(BuildDraft());
            SetStatus(result.Message, !result.IsSuccess);
            if (result.IsSuccess && result.Profile is not null)
            {
                Password = string.Empty;
                await ReloadAsync(result.Profile.Id);
            }
        });
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        await RunBusyAsync(async () =>
        {
            ConnectionTestResult result = await _profileService.TestConnectionAsync(BuildDraft());
            SetStatus(result.Message, !result.IsSuccess);
        });
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        IsImporting = true;
        try
        {
            await RunBusyAsync(async () =>
            {
                ConnectionTestResult result = await _profileService.ConnectAsync(BuildDraft(), cancellationToken);
                SetStatus(result.Message, !result.IsSuccess);
                if (result.IsSuccess)
                {
                    Password = string.Empty;
                    await ReloadAsync(SelectedProfile?.Id);
                }
            });
        }
        finally
        {
            IsImporting = false;
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedProfile))]
    private async Task DeleteAsync()
    {
        if (SelectedProfile is null ||
            !_notifications.Confirm(
                "Delete profile",
                $"Delete the profile ‘{SelectedProfile.Name}’? This cannot be undone."))
        {
            return;
        }

        Guid id = SelectedProfile.Id;
        await RunBusyAsync(async () =>
        {
            await _profileService.DeleteAsync(id);
            await ReloadAsync(profileIdToSelect: null);
            ClearEditor();
            SetStatus("Profile deleted.", isError: false);
        });
    }

    [RelayCommand]
    private void BrowsePlaylist()
    {
        string? selectedPath = _filePicker.PickPlaylistFile();
        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            ServerAddress = selectedPath;
        }
    }

    partial void OnSelectedProfileChanged(IptvProfile? value)
    {
        DeleteCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(HasSelectedProfile));

        if (value is null)
        {
            return;
        }

        ProfileName = value.Name;
        SelectedProfileType = ProfileTypes.Single(option => option.Value == value.ConnectionType);
        ServerAddress = value.ServerAddress;
        Username = value.Username ?? string.Empty;
        Password = string.Empty;
        SetStatus("Editing profile. Leave password blank to keep the saved Windows-protected password.", isError: false);
    }

    partial void OnSelectedProfileTypeChanged(ProfileTypeOption value)
    {
        OnPropertyChanged(nameof(ShowsCredentialFields));
        OnPropertyChanged(nameof(ShowsBrowseButton));
        if (value.Value == ProfileConnectionType.M3uPlaylist)
        {
            Username = string.Empty;
            Password = string.Empty;
        }
    }

    private ProfileDraft BuildDraft() =>
        new()
        {
            Id = SelectedProfile?.Id,
            Name = ProfileName,
            ConnectionType = SelectedProfileType.Value,
            ServerAddress = ServerAddress,
            Username = ShowsCredentialFields ? Username : null,
            Password = ShowsCredentialFields ? Password : null,
        };

    private async Task ReloadAsync(Guid? profileIdToSelect)
    {
        IsBusy = true;
        try
        {
            IReadOnlyList<IptvProfile> profiles = await _profileService.GetAllAsync();
            Profiles.Clear();
            foreach (IptvProfile profile in profiles)
            {
                Profiles.Add(profile);
            }

            OnPropertyChanged(nameof(HasProfiles));
            SelectedProfile = profileIdToSelect.HasValue
                ? Profiles.FirstOrDefault(profile => profile.Id == profileIdToSelect.Value)
                : Profiles.FirstOrDefault();
        }
        catch (Exception exception)
        {
            SetStatus("Profiles could not be loaded.", isError: true);
            _notifications.ShowError("Profiles", "Profiles could not be loaded. Technical details were logged safely.");
            LogProfileOperationFailed(exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RunBusyAsync(Func<Task> action)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await action();
        }
        catch (OperationCanceledException)
        {
            SetStatus("Operation canceled.", isError: false);
        }
        catch (Exception exception)
        {
            SetStatus("The operation could not be completed.", isError: true);
            _notifications.ShowError("Profiles", "The operation could not be completed. Technical details were logged safely.");
            LogProfileOperationFailed(exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ClearEditor()
    {
        ProfileName = string.Empty;
        SelectedProfileType = ProfileTypes[0];
        ServerAddress = string.Empty;
        Username = string.Empty;
        Password = string.Empty;
    }

    private void SetStatus(string message, bool isError)
    {
        StatusMessage = message;
        IsStatusError = isError;
    }

    [LoggerMessage(
        EventId = 4001,
        Level = LogLevel.Error,
        Message = "A profile-management operation failed.")]
    private partial void LogProfileOperationFailed(Exception exception);
}
