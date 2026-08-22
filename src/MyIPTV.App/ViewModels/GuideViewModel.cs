namespace MyIPTV.App.ViewModels;

public sealed class GuideViewModel()
    : SectionViewModel(
        "TV Guide",
        "See current and upcoming programs from XMLTV data.",
        "No guide data yet",
        "Connect a profile with EPG data or configure an XMLTV source.",
        "\uE787");
