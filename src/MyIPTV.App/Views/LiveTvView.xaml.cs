using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using LibVLCSharp.Shared;
using MyIPTV.App.ViewModels;

namespace MyIPTV.App.Views;

public partial class LiveTvView : UserControl
{
    private WindowState _previousState;
    private WindowStyle _previousStyle;
    private ResizeMode _previousResizeMode;
    private Thickness _previousLayoutMargin;
    private Thickness _previousContentMargin;
    private bool _previousTopmost;
    private Rect _previousBounds;

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
            viewModel.Player.FullScreenChanged -= OnFullScreenChanged;
            viewModel.Player.FullScreenChanged += OnFullScreenChanged;
            ReattachVideoSurface(viewModel);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // LibVLCSharp renders through a native child window. Detaching it when this
        // WPF view leaves the visual tree prevents that HWND from covering the next tab.
        VideoSurface.MediaPlayer = null;

        if (DataContext is LiveTvViewModel viewModel)
        {
            if (viewModel.Player.IsFullScreen)
            {
                viewModel.Player.ToggleFullScreenCommand.Execute(null);
            }

            viewModel.Player.FullScreenChanged -= OnFullScreenChanged;
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
            _previousTopmost = window.Topmost;
            _previousBounds = new Rect(window.Left, window.Top, window.Width, window.Height);
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
            Rect monitorBounds = GetMonitorBounds(window);
            window.WindowState = WindowState.Normal;
            window.WindowStyle = WindowStyle.None;
            window.ResizeMode = ResizeMode.NoResize;
            window.Topmost = true;
            window.Left = monitorBounds.Left;
            window.Top = monitorBounds.Top;
            window.Width = monitorBounds.Width;
            window.Height = monitorBounds.Height;
        }
        else
        {
            Grid.SetRow(ContentScroller, 1);
            Grid.SetRowSpan(ContentScroller, 1);
            LayoutRoot.Margin = _previousLayoutMargin;
            ContentScroller.Margin = _previousContentMargin;
            VideoPanel.Margin = new Thickness(7, 0, 0, 0);
            CategoryColumn.MinWidth = 220;
            ChannelColumn.MinWidth = 240;
            CategoryColumn.Width = new GridLength(300);
            CategorySplitterColumn.Width = new GridLength(5);
            ChannelColumn.Width = new GridLength(300);
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
            window.Topmost = _previousTopmost;
            window.WindowState = WindowState.Normal;
            window.Left = _previousBounds.Left;
            window.Top = _previousBounds.Top;
            window.Width = _previousBounds.Width;
            window.Height = _previousBounds.Height;
            window.WindowState = _previousState;
        }
    }

    private static Rect GetMonitorBounds(Window window)
    {
        nint windowHandle = new WindowInteropHelper(window).Handle;
        nint monitorHandle = MonitorFromWindow(windowHandle, MonitorDefaultToNearest);
        MonitorInfo monitorInfo = new() { Size = Marshal.SizeOf<MonitorInfo>() };
        if (monitorHandle == 0 || !GetMonitorInfo(monitorHandle, ref monitorInfo))
        {
            return new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
        }

        Point topLeft = new(monitorInfo.Monitor.Left, monitorInfo.Monitor.Top);
        Point bottomRight = new(monitorInfo.Monitor.Right, monitorInfo.Monitor.Bottom);
        if (PresentationSource.FromVisual(window)?.CompositionTarget is { } compositionTarget)
        {
            topLeft = compositionTarget.TransformFromDevice.Transform(topLeft);
            bottomRight = compositionTarget.TransformFromDevice.Transform(bottomRight);
        }

        return new Rect(topLeft, bottomRight);
    }

    private const uint MonitorDefaultToNearest = 2;

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint windowHandle, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitorHandle, ref MonitorInfo monitorInfo);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect WorkArea;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private void OnChannelSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ChannelList.SelectedItem is not null)
        {
            Dispatcher.BeginInvoke(
                () => ChannelList.ScrollIntoView(ChannelList.SelectedItem),
                DispatcherPriority.Loaded);
        }
    }

    private void ReattachVideoSurface(LiveTvViewModel viewModel)
    {
        if (viewModel.Player.NativeMediaPlayer is not MediaPlayer mediaPlayer)
        {
            return;
        }

        VideoSurface.MediaPlayer = null;
        Dispatcher.BeginInvoke(() =>
        {
            if (!IsLoaded || !ReferenceEquals(DataContext, viewModel))
            {
                return;
            }

            VideoSurface.MediaPlayer = mediaPlayer;
            VideoSurface.InvalidateVisual();
        }, DispatcherPriority.Loaded);
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
