namespace MyIPTV.Core.Abstractions;

public interface IUserNotificationService
{
    void ShowError(string title, string message);
}
