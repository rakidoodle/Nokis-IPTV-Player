using System.Windows;
using MyIPTV.App.ViewModels;

namespace MyIPTV.App;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
