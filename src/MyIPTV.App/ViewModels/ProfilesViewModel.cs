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
            new("M3U", ProfileConnectionType.M3uPlaylist),
            new("Xtream", ProfileConnectionType.XtreamApi),
            new("Stalker", ProfileConnectionType.StalkerPortal),
        ];
        _selectedProfileType = ProfileTypes[0];
    }

    public ObservableCollection<IptvProfile> Profiles { get; } = [];

    public IReadOnlyList<ProfileTypeOption> ProfileTypes { get; }

    public bool HasProfiles => Profiles.Count > 0;

    public bool HasSelectedProfile => SelectedProfile is not null;

    public bool ShowsUsernameField =>
        SelectedProfileType.Value is ProfileConnectionType.XtreamApi or ProfileConnectionType.StalkerPortal;

    public bool ShowsPasswordField => SelectedProfileType.Value == ProfileConnectionType.XtreamApi;

    public string AddressLabel => SelectedProfileType.Value switch
    {
        ProfileConnectionType.M3uPlaylist => "M3U link or local playlist",
        ProfileConnectionType.XtreamApi => "Server link",
        _ => "Portal link / URL",
    };

    public string AddressPlaceholder => SelectedProfileType.Value switch
    {
        ProfileConnectionType.M3uPlaylist => "https://provider.example/get.php?username=…&password=…",
        ProfileConnectionType.XtreamApi => "https://provider.example:port",
        _ => "https://provider.example/stalker_portal/c/",
    };

    public string UsernameLabel =>
        SelectedProfileType.Value == ProfileConnectionType.StalkerPortal ? "MAC address" : "Username";

    public string UsernamePlaceholder =>
        SelectedProfileType.Value == ProfileConnectionType.StalkerPortal ? "00:1A:79:00:00:00" : "Account username";

    public bool ShowsBrowseButton =>
        SelectedProfileType.Value == ProfileConnectionType.M3uPlaylist;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        await ReloadAsync(SelectedProfile?.Id);
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
            else if (string.Equals(
                         result.Message,
                         "A profile with this name already exists.",
                         StringComparison.Ordinal))
            {
                string requestedName = ProfileName.Trim();
                await ReloadAsync(profileIdToSelect: null);
                SelectedProfile = Profiles.FirstOrDefault(profile =>
                    string.Equals(profile.Name.Trim(), requestedName, StringComparison.OrdinalIgnoreCase));
                SetStatus("That profile already exists and has been selected.", isError: true);
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

    async partial void OnSelectedProfileChanged(IptvProfile? value)
    {
        DeleteCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(HasSelectedProfile));

        if (value is null)
        {
            return;
        }

        ProfileDraft? draft = await _profileService.GetDraftAsync(value.Id);
        if (draft is null || SelectedProfile?.Id != value.Id)
        {
            return;
        }

        ProfileName = draft.Name;
        SelectedProfileType = ProfileTypes.Single(option => option.Value == draft.ConnectionType);
        ServerAddress = draft.ServerAddress;
        Username = draft.Username ?? string.Empty;
        Password = draft.Password ?? string.Empty;
        SetStatus("Editing profile.", isError: false);
    }

    partial void OnSelectedProfileTypeChanged(ProfileTypeOption value)
    {
        OnPropertyChanged(nameof(ShowsUsernameField));
        OnPropertyChanged(nameof(ShowsPasswordField));
        OnPropertyChanged(nameof(ShowsBrowseButton));
        OnPropertyChanged(nameof(AddressLabel));
        OnPropertyChanged(nameof(AddressPlaceholder));
        OnPropertyChanged(nameof(UsernameLabel));
        OnPropertyChanged(nameof(UsernamePlaceholder));
    }

    private ProfileDraft BuildDraft() =>
        new()
        {
            Id = SelectedProfile?.Id,
            Name = ProfileName,
            ConnectionType = SelectedProfileType.Value,
            ServerAddress = NormalizePastedAddress(ServerAddress),
            Username = Username,
            Password = Password,
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

    private static string NormalizePastedAddress(string address) =>
        address.Trim()
            .Trim('"', '\'', '<', '>')
            .Replace("&amp;", "&", StringComparison.OrdinalIgnoreCase)
            .Replace("\\&", "&", StringComparison.Ordinal);

    [LoggerMessage(
        EventId = 4001,
        Level = LogLevel.Error,
        Message = "A profile-management operation failed.")]
    private partial void LogProfileOperationFailed(Exception exception);
}
