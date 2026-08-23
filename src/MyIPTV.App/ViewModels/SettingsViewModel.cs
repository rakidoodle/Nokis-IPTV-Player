using System.Diagnostics;
using System.IO;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyIPTV.App.Services;
using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;

namespace MyIPTV.App.ViewModels;

public sealed partial class SettingsViewModel : SectionViewModel
{
    private readonly ISettingsService _settingsService;
    private readonly IThemeService _themeService;
    private readonly IPlaybackService _playbackService;
    private readonly IWatchHistoryRepository _historyRepository;
    private readonly IDataMaintenanceService _dataMaintenanceService;
    private readonly IUserNotificationService _notifications;
    private readonly IApplicationPaths _paths;
    private readonly IDevelopmentDataService _developmentData;

    [ObservableProperty] private string _startPage = "Home";
    [ObservableProperty] private string _theme = "Dark";
    [ObservableProperty] private string _language = "en-US";
    [ObservableProperty] private bool _rememberLastProfile = true;
    [ObservableProperty] private int _defaultVolume = 80;
    [ObservableProperty] private bool _hardwareDecoding = true;
    [ObservableProperty] private string _aspectRatio = "Default";
    [ObservableProperty] private bool _reconnectOnFailure = true;
    [ObservableProperty] private string _epgSource = string.Empty;
    [ObservableProperty] private int _epgRefreshHours = 6;
    [ObservableProperty] private string _epgTimezoneBehavior = "Local";
    [ObservableProperty] private string _statusMessage = "Loading preferences…";
    [ObservableProperty] private bool _isBusy;

    public SettingsViewModel(
        ISettingsService settingsService,
        IThemeService themeService,
        IPlaybackService playbackService,
        IWatchHistoryRepository historyRepository,
        IDataMaintenanceService dataMaintenanceService,
        IDevelopmentDataService developmentData,
        IUserNotificationService notifications,
        IApplicationPaths paths)
        : base("Settings", "Manage appearance, playback, EPG, and local application data.",
            "Settings unavailable", "Preferences could not be loaded.", "\uE713")
    {
        _settingsService = settingsService;
        _themeService = themeService;
        _playbackService = playbackService;
        _historyRepository = historyRepository;
        _dataMaintenanceService = dataMaintenanceService;
        _developmentData = developmentData;
        _notifications = notifications;
        _paths = paths;
        ApplicationVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "Development";
        _ = InitializeAsync();
    }

    public IReadOnlyList<string> StartPages { get; } = ["Home", "Live TV", "Movies", "Series", "Favorites", "Guide"];
    public IReadOnlyList<string> Themes { get; } = ["Dark", "Light", "Midnight", "Ocean"];
    public IReadOnlyList<string> Languages { get; } = ["en-US"];
    public IReadOnlyList<string> AspectRatios { get; } = ["Default", "16:9", "4:3", "21:9", "1:1"];
    public IReadOnlyList<string> TimezoneBehaviors { get; } = ["Local", "UTC"];
    public string ApplicationVersion { get; }
    public string DatabaseLocation => _paths.DatabasePath;
    public string LogsLocation => _paths.LogsDirectory;
    public string DemoDataButtonText => _developmentData.IsLoaded ? "Remove demo library" : "Load demo library";

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        try
        {
            AppSettings settings = new()
            {
                StartPage = StartPage,
                Theme = Theme,
                Language = Language,
                RememberLastProfile = RememberLastProfile,
                DefaultVolume = DefaultVolume,
                HardwareDecoding = HardwareDecoding,
                AspectRatio = AspectRatio,
                ReconnectOnFailure = ReconnectOnFailure,
                EpgSource = EpgSource.Trim(),
                EpgRefreshHours = EpgRefreshHours,
                EpgTimezoneBehavior = EpgTimezoneBehavior,
            };
            await _settingsService.SaveAsync(settings, cancellationToken);
            _themeService.ApplyTheme(Enum.TryParse(Theme, true, out AppTheme selectedTheme)
                ? selectedTheme : AppTheme.Dark);
            _playbackService.SetVolume(DefaultVolume);
            _playbackService.SetAspectRatio(AspectRatio);
            StatusMessage = HardwareDecoding
                ? "Preferences saved. Hardware decoding applies to new player sessions."
                : "Preferences saved.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            StatusMessage = "Preferences could not be saved.";
            _notifications.ShowError("Settings", "Noki's IPTV Player could not save these preferences. Check the data folder permissions.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ClearCacheAsync()
    {
        IsBusy = true;
        try
        {
            await _dataMaintenanceService.ClearCacheAsync();
            StatusMessage = "Disposable cache files cleared.";
        }
        catch (IOException)
        {
            StatusMessage = "Some cache files are currently in use.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ClearWatchHistoryAsync()
    {
        if (!_notifications.Confirm("Clear watch history", "Remove all saved playback progress? This cannot be undone."))
        {
            return;
        }

        await _historyRepository.ClearAsync();
        StatusMessage = "Watch history cleared.";
    }

    [RelayCommand]
    private async Task ToggleDemoDataAsync()
    {
        IsBusy = true;
        try
        {
            if (_developmentData.IsLoaded)
            {
                await _developmentData.RemoveAsync();
                StatusMessage = "Development demo library removed.";
            }
            else
            {
                await _developmentData.LoadAsync();
                StatusMessage = "Safe demo channels, movies, and series are ready.";
            }
            OnPropertyChanged(nameof(DemoDataButtonText));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenDatabaseFolder() => OpenFolder(_paths.DataDirectory);

    [RelayCommand]
    private void OpenLogsFolder() => OpenFolder(_paths.LogsDirectory);

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            AppSettings settings = await _settingsService.LoadAsync(cancellationToken);
            StartPage = settings.StartPage;
            Theme = settings.Theme;
            Language = settings.Language;
            RememberLastProfile = settings.RememberLastProfile;
            DefaultVolume = settings.DefaultVolume;
            HardwareDecoding = settings.HardwareDecoding;
            AspectRatio = settings.AspectRatio;
            ReconnectOnFailure = settings.ReconnectOnFailure;
            EpgSource = settings.EpgSource;
            EpgRefreshHours = settings.EpgRefreshHours;
            EpgTimezoneBehavior = settings.EpgTimezoneBehavior;
            StatusMessage = "Preferences are ready.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            StatusMessage = "Preferences could not be read; safe defaults are shown.";
        }
    }

    private void OpenFolder(string path)
    {
        try
        {
            _paths.EnsureDirectoriesExist();
            Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _notifications.ShowError("Open folder", "Windows could not open this application data folder.");
        }
    }
}
