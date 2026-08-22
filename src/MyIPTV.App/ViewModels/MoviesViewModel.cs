using MyIPTV.Core.Abstractions;

namespace MyIPTV.App.ViewModels;

public sealed class MoviesViewModel : SectionViewModel
{
    private readonly IMediaCatalog _mediaCatalog;

    public MoviesViewModel(IMediaCatalog mediaCatalog)
        : base(
            "Movies",
            "Browse movies supplied by your connected provider.",
            "No movies yet",
            "Movies will appear here when a supported profile is connected.",
            "\uE8B2")
    {
        _mediaCatalog = mediaCatalog;
        mediaCatalog.CatalogChanged += OnCatalogChanged;
        UpdateSummary();
    }

    private void OnCatalogChanged(object? sender, EventArgs e) => UpdateSummary();

    private void UpdateSummary()
    {
        int count = _mediaCatalog.GetMovies().Count;
        SetEmptyContent(
            count == 0 ? "No movies yet" : $"{count:N0} {(count == 1 ? "movie" : "movies")} loaded",
            count == 0
                ? "Connect an Xtream profile to load movies."
                : "Your movie catalog is ready. The full browser arrives in Phase 16.");
    }
}
