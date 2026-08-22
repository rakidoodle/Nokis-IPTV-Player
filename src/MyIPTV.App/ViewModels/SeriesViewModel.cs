using MyIPTV.Core.Abstractions;

namespace MyIPTV.App.ViewModels;

public sealed class SeriesViewModel : SectionViewModel
{
    private readonly IMediaCatalog _mediaCatalog;

    public SeriesViewModel(IMediaCatalog mediaCatalog)
        : base(
            "Series",
            "Browse shows, seasons, and episodes.",
            "No series yet",
            "Series will appear here when a supported profile is connected.",
            "\uE8D6")
    {
        _mediaCatalog = mediaCatalog;
        mediaCatalog.CatalogChanged += OnCatalogChanged;
        UpdateSummary();
    }

    private void OnCatalogChanged(object? sender, EventArgs e) => UpdateSummary();

    private void UpdateSummary()
    {
        int count = _mediaCatalog.GetSeries().Count;
        SetEmptyContent(
            count == 0 ? "No series yet" : $"{count:N0} series loaded",
            count == 0
                ? "Connect an Xtream profile to load series."
                : "Your series catalog is ready. Seasons and episodes arrive in Phase 17.");
    }
}
