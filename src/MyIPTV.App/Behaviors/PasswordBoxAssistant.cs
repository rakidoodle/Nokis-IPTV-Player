using System.Windows;
using System.Windows.Controls;

namespace MyIPTV.App.Behaviors;

public static class PasswordBoxAssistant
{
    public static readonly DependencyProperty BindPasswordProperty = DependencyProperty.RegisterAttached(
        "BindPassword",
        typeof(bool),
        typeof(PasswordBoxAssistant),
        new PropertyMetadata(false, OnBindPasswordChanged));

    public static readonly DependencyProperty BoundPasswordProperty = DependencyProperty.RegisterAttached(
        "BoundPassword",
        typeof(string),
        typeof(PasswordBoxAssistant),
        new FrameworkPropertyMetadata(
            string.Empty,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnBoundPasswordChanged));

    private static readonly DependencyProperty IsUpdatingProperty = DependencyProperty.RegisterAttached(
        "IsUpdating",
        typeof(bool),
        typeof(PasswordBoxAssistant));

    public static bool GetBindPassword(DependencyObject element) =>
        (bool)element.GetValue(BindPasswordProperty);

    public static void SetBindPassword(DependencyObject element, bool value) =>
        element.SetValue(BindPasswordProperty, value);

    public static string GetBoundPassword(DependencyObject element) =>
        (string)element.GetValue(BoundPasswordProperty);

    public static void SetBoundPassword(DependencyObject element, string value) =>
        element.SetValue(BoundPasswordProperty, value);

    private static bool GetIsUpdating(DependencyObject element) =>
        (bool)element.GetValue(IsUpdatingProperty);

    private static void SetIsUpdating(DependencyObject element, bool value) =>
        element.SetValue(IsUpdatingProperty, value);

    private static void OnBindPasswordChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not PasswordBox passwordBox)
        {
            return;
        }

        if ((bool)e.OldValue)
        {
            passwordBox.PasswordChanged -= OnPasswordChanged;
        }

        if ((bool)e.NewValue)
        {
            passwordBox.PasswordChanged += OnPasswordChanged;
        }
    }

    private static void OnBoundPasswordChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not PasswordBox passwordBox || GetIsUpdating(passwordBox))
        {
            return;
        }

        passwordBox.PasswordChanged -= OnPasswordChanged;
        passwordBox.Password = e.NewValue as string ?? string.Empty;
        passwordBox.PasswordChanged += OnPasswordChanged;
    }

    private static void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        PasswordBox passwordBox = (PasswordBox)sender;
        SetIsUpdating(passwordBox, true);
        SetBoundPassword(passwordBox, passwordBox.Password);
        SetIsUpdating(passwordBox, false);
    }
}
