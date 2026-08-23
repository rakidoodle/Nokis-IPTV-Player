using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;

namespace MyIPTV.App.ViewModels;

public sealed partial class GuideViewModel : SectionViewModel
{
    private readonly IEpgService _epgService;
    private readonly ISettingsService _settingsService;
    private int _refreshHours = 6;
    private bool _displayLocalTime = true;

    [ObservableProperty]
    private string _source = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Enter an XMLTV web address or local file path.";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private IReadOnlyList<GuideChannelRowViewModel> _rows = [];

    public GuideViewModel(IEpgService epgService, ISettingsService settingsService)
        : base("Program Guide", "See current and upcoming programs from an XMLTV source.",
            "No guide loaded", "Enter an XMLTV source above, then choose Refresh guide.", "\uE787")
    {
        _epgService = epgService;
        _settingsService = settingsService;
        epgService.EpgChanged += OnEpgChanged;
        _ = InitializeAsync();
    }

    public bool HasRows => Rows.Count > 0;

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task RefreshGuideAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        try
        {
            EpgRefreshResult result = await _epgService.RefreshAsync(
                Source, TimeSpan.FromHours(_refreshHours), cancellationToken);
            AppSettings settings = await _settingsService.LoadAsync(cancellationToken);
            await _settingsService.SaveAsync(settings with { EpgSource = Source.Trim() }, cancellationToken);
            StatusMessage = result.Message;
            await LoadRowsAsync(cancellationToken);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void OnEpgChanged(object? sender, EventArgs e) => await LoadRowsAsync();

    private async Task LoadRowsAsync(CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        IReadOnlyList<EpgChannelSchedule> schedules = await _epgService.GetGuideAsync(
            now.AddHours(-1), now.AddHours(6), cancellationToken);
        Rows = schedules.Select(schedule => new GuideChannelRowViewModel(
            schedule.Channel.Id,
            schedule.Channel.DisplayName,
            schedule.Programs.Select(program => new GuideProgramViewModel(program, _displayLocalTime)).ToArray()))
            .ToArray();
        OnPropertyChanged(nameof(HasRows));
    }

    private async Task InitializeAsync()
    {
        AppSettings settings = await _settingsService.LoadAsync();
        Source = settings.EpgSource;
        _refreshHours = settings.EpgRefreshHours;
        _displayLocalTime = !string.Equals(settings.EpgTimezoneBehavior, "UTC", StringComparison.OrdinalIgnoreCase);
    }
}
