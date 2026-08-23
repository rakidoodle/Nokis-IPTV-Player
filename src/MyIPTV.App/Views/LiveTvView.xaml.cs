using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MyIPTV.App.ViewModels;

namespace MyIPTV.App.Views;

public partial class LiveTvView : UserControl
{
    private WindowState _previousState;
    private WindowStyle _previousStyle;
    private ResizeMode _previousResizeMode;
    private Thickness _previousLayoutMargin;
    private Thickness _previousContentMargin;

    public LiveTvView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is LiveTvViewModel viewModel)
        {
            viewModel.Player.FullScreenChanged += OnFullScreenChanged;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is LiveTvViewModel viewModel)
        {
            viewModel.Player.FullScreenChanged -= OnFullScreenChanged;
            if (viewModel.Player.IsFullScreen)
            {
                viewModel.Player.ToggleFullScreenCommand.Execute(null);
            }
        }
    }

    private void OnFullScreenChanged(object? sender, EventArgs e)
    {
        if (sender is not PlayerViewModel player || Window.GetWindow(this) is not Window window)
        {
            return;
        }

        if (player.IsFullScreen)
        {
            _previousState = window.WindowState;
            _previousStyle = window.WindowStyle;
            _previousResizeMode = window.ResizeMode;
            _previousLayoutMargin = LayoutRoot.Margin;
            _previousContentMargin = ContentScroller.Margin;
            if (window is MainWindow mainWindow)
            {
                mainWindow.EnterContentFullScreen();
            }

            PageHeading.Visibility = Visibility.Collapsed;
            CategoryPanel.Visibility = Visibility.Collapsed;
            CategorySplitter.Visibility = Visibility.Collapsed;
            ChannelPanel.Visibility = Visibility.Collapsed;
            ChannelSplitter.Visibility = Visibility.Collapsed;
            ProgramDetailsPanel.Visibility = Visibility.Collapsed;
            PlaybackControlsPanel.Visibility = Visibility.Collapsed;
            CategoryColumn.MinWidth = 0;
            ChannelColumn.MinWidth = 0;
            CategoryColumn.Width = new GridLength(0);
            CategorySplitterColumn.Width = new GridLength(0);
            ChannelColumn.Width = new GridLength(0);
            ChannelSplitterColumn.Width = new GridLength(0);
            VideoPanel.Margin = new Thickness(0);
            LayoutRoot.Margin = new Thickness(0);
            ContentScroller.Margin = new Thickness(0);
            Grid.SetRow(ContentScroller, 0);
            Grid.SetRowSpan(ContentScroller, 3);
            window.WindowStyle = WindowStyle.None;
            window.ResizeMode = ResizeMode.NoResize;
            window.WindowState = WindowState.Maximized;
        }
        else
        {
            Grid.SetRow(ContentScroller, 1);
            Grid.SetRowSpan(ContentScroller, 1);
            LayoutRoot.Margin = _previousLayoutMargin;
            ContentScroller.Margin = _previousContentMargin;
            VideoPanel.Margin = new Thickness(7, 0, 0, 0);
            CategoryColumn.MinWidth = 120;
            ChannelColumn.MinWidth = 170;
            CategoryColumn.Width = new GridLength(165);
            CategorySplitterColumn.Width = new GridLength(5);
            ChannelColumn.Width = new GridLength(250);
            ChannelSplitterColumn.Width = new GridLength(5);
            PageHeading.Visibility = Visibility.Visible;
            CategoryPanel.Visibility = Visibility.Visible;
            CategorySplitter.Visibility = Visibility.Visible;
            ChannelPanel.Visibility = Visibility.Visible;
            ChannelSplitter.Visibility = Visibility.Visible;
            ProgramDetailsPanel.Visibility = Visibility.Visible;
            PlaybackControlsPanel.Visibility = Visibility.Visible;
            if (window is MainWindow mainWindow)
            {
                mainWindow.ExitContentFullScreen();
            }

            window.WindowStyle = _previousStyle;
            window.ResizeMode = _previousResizeMode;
            window.WindowState = _previousState;
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DataContext is LiveTvViewModel { Player.IsFullScreen: true } viewModel)
        {
            viewModel.Player.ToggleFullScreenCommand.Execute(null);
            e.Handled = true;
        }
    }
}
