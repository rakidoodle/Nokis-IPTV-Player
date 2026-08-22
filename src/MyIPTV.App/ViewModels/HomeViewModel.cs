using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyIPTV.Core.Abstractions;

namespace MyIPTV.App.ViewModels;

public sealed partial class HomeViewModel(INavigationService navigationService) : ObservableObject
{
    public string Heading { get; } = "Good to see you";

    public string Description { get; } =
        "Connect a legitimate IPTV source to start building your personal media library.";

    [RelayCommand]
    private void BrowseLiveTv()
    {
        navigationService.NavigateTo<LiveTvViewModel>();
    }

    [RelayCommand]
    private void ManageProfiles()
    {
        navigationService.NavigateTo<ProfilesViewModel>();
    }
}
