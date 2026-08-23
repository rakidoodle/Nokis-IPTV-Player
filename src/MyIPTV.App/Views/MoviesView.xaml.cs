using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MyIPTV.App.ViewModels;

namespace MyIPTV.App.Views;

public partial class MoviesView : UserControl
{
    private WindowState _previousState;
    private WindowStyle _previousStyle;
    private ResizeMode _previousResizeMode;

    public MoviesView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MoviesViewModel viewModel)
        {
            viewModel.Player.FullScreenChanged += OnFullScreenChanged;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MoviesViewModel viewModel)
        {
            viewModel.Player.FullScreenChanged -= OnFullScreenChanged;
            if (viewModel.Player.IsFullScreen) viewModel.Player.ToggleFullScreenCommand.Execute(null);
        }
    }

    private void OnFullScreenChanged(object? sender, EventArgs e)
    {
        if (sender is not PlayerViewModel player || Window.GetWindow(this) is not Window window) return;
        if (player.IsFullScreen)
        {
            _previousState = window.WindowState;
            _previousStyle = window.WindowStyle;
            _previousResizeMode = window.ResizeMode;
            window.WindowStyle = WindowStyle.None;
            window.ResizeMode = ResizeMode.NoResize;
            window.WindowState = WindowState.Maximized;
        }
        else
        {
            window.WindowStyle = _previousStyle;
            window.ResizeMode = _previousResizeMode;
            window.WindowState = _previousState;
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DataContext is MoviesViewModel { Player.IsFullScreen: true } viewModel)
        {
            viewModel.Player.ToggleFullScreenCommand.Execute(null);
            e.Handled = true;
        }
    }
}
