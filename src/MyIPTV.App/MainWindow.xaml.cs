using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MyIPTV.App.ViewModels;

namespace MyIPTV.App;

public partial class MainWindow : Window
{
    private GridLength _savedSidebarWidth;

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        StateChanged += OnWindowStateChanged;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && SearchBox.IsKeyboardFocusWithin)
        {
            Keyboard.ClearFocus();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape &&
                 DataContext is MainWindowViewModel { CurrentViewModel: LiveTvViewModel liveTv } &&
                 liveTv.Player.IsFullScreen)
        {
            liveTv.Player.ToggleFullScreenCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnSearchBoxGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel &&
            viewModel.SearchText.Trim().Length >= 2)
        {
            viewModel.IsSearchOpen = true;
        }
    }

    private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsInsideControl(e.OriginalSource as DependencyObject))
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            ToggleMaximizeRestore();
        }
        else if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e) =>
        SystemCommands.MinimizeWindow(this);

    private void OnMaximizeRestoreClick(object sender, RoutedEventArgs e) =>
        ToggleMaximizeRestore();

    private void OnCloseClick(object sender, RoutedEventArgs e) =>
        SystemCommands.CloseWindow(this);

    private void OnWindowStateChanged(object? sender, EventArgs e) =>
        MaximizeGlyph.Text = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";

    private void ToggleMaximizeRestore()
    {
        if (WindowState == WindowState.Maximized)
        {
            SystemCommands.RestoreWindow(this);
        }
        else
        {
            SystemCommands.MaximizeWindow(this);
        }
    }

    internal void EnterContentFullScreen()
    {
        _savedSidebarWidth = SidebarColumn.Width;
        SidebarPanel.Visibility = Visibility.Collapsed;
        TopNavigationPanel.Visibility = Visibility.Collapsed;
        StatusFooterPanel.Visibility = Visibility.Collapsed;
        SidebarColumn.Width = new GridLength(0);
        HeaderRow.Height = new GridLength(0);
        FooterRow.Height = new GridLength(0);
        Grid.SetColumn(MainContentGrid, 0);
        Grid.SetColumnSpan(MainContentGrid, 2);
    }

    internal void ExitContentFullScreen()
    {
        Grid.SetColumn(MainContentGrid, 1);
        Grid.SetColumnSpan(MainContentGrid, 1);
        SidebarColumn.Width = _savedSidebarWidth;
        HeaderRow.Height = new GridLength(74);
        FooterRow.Height = new GridLength(52);
        SidebarPanel.Visibility = Visibility.Visible;
        TopNavigationPanel.Visibility = Visibility.Visible;
        StatusFooterPanel.Visibility = Visibility.Visible;
    }

    private bool IsInsideControl(DependencyObject? source)
    {
        for (DependencyObject? current = source; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (ReferenceEquals(current, this))
            {
                return false;
            }

            if (current is System.Windows.Controls.Control)
            {
                return true;
            }
        }

        return false;
    }
}
