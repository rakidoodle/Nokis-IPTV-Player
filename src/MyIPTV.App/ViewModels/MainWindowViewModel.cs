using CommunityToolkit.Mvvm.ComponentModel;
using MyIPTV.Core.Abstractions;

namespace MyIPTV.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private object? _currentViewModel;

    public MainWindowViewModel(
        INavigationService navigationService,
        IApplicationConfiguration configuration)
    {
        _navigationService = navigationService;
        ApplicationName = configuration.Application.Name;
        CurrentViewModel = navigationService.CurrentViewModel;
        navigationService.CurrentViewModelChanged += OnCurrentViewModelChanged;
    }

    public string ApplicationName { get; }

    public string StatusMessage { get; } = "Ready";

    private void OnCurrentViewModelChanged(object? sender, EventArgs e)
    {
        CurrentViewModel = _navigationService.CurrentViewModel;
    }
}
