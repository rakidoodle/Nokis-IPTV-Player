using CommunityToolkit.Mvvm.ComponentModel;

namespace MyIPTV.App.ViewModels;

public sealed class HomeViewModel : ObservableObject
{
    public string Heading { get; } = "Welcome to MyIPTV";

    public string Description { get; } =
        "The application foundation is ready. Add an authorized IPTV profile in a later phase.";
}
