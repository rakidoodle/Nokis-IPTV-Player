using CommunityToolkit.Mvvm.ComponentModel;

namespace MyIPTV.App.ViewModels;

public abstract partial class SectionViewModel(
    string title,
    string subtitle,
    string emptyTitle,
    string emptyMessage,
    string emptyGlyph) : ObservableObject
{
    [ObservableProperty]
    private ViewState _state = ViewState.Empty;

    public string Title { get; } = title;

    public string Subtitle { get; } = subtitle;

    public string EmptyTitle { get; } = emptyTitle;

    public string EmptyMessage { get; } = emptyMessage;

    public string EmptyGlyph { get; } = emptyGlyph;
}
