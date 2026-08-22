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

    [ObservableProperty]
    private string _emptyTitle = emptyTitle;

    [ObservableProperty]
    private string _emptyMessage = emptyMessage;

    public string Title { get; } = title;

    public string Subtitle { get; } = subtitle;

    public string EmptyGlyph { get; } = emptyGlyph;

    protected void SetEmptyContent(string title, string message)
    {
        EmptyTitle = title;
        EmptyMessage = message;
    }
}
