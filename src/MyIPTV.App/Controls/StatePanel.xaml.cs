using System.Windows;
using System.Windows.Controls;
using MyIPTV.App.ViewModels;

namespace MyIPTV.App.Controls;

public partial class StatePanel : UserControl
{
    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State),
        typeof(ViewState),
        typeof(StatePanel),
        new PropertyMetadata(ViewState.Empty));

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(string),
        typeof(StatePanel),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(
        nameof(Message),
        typeof(string),
        typeof(StatePanel),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
        nameof(Glyph),
        typeof(string),
        typeof(StatePanel),
        new PropertyMetadata("\uE946"));

    public StatePanel()
    {
        InitializeComponent();
    }

    public ViewState State
    {
        get => (ViewState)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }
}
