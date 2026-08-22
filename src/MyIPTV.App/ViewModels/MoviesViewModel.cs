namespace MyIPTV.App.ViewModels;

public sealed class MoviesViewModel()
    : SectionViewModel(
        "Movies",
        "Browse movies supplied by your connected provider.",
        "No movies yet",
        "Movies will appear here when a supported profile is connected.",
        "\uE8B2");
