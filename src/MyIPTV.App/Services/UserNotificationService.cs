using System.Windows;
using MyIPTV.Core.Abstractions;

namespace MyIPTV.App.Services;

public sealed class UserNotificationService : IUserNotificationService
{
    public void ShowError(string title, string message)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
