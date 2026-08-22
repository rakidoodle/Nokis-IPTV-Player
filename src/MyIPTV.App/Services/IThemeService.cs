namespace MyIPTV.App.Services;

public interface IThemeService
{
    AppTheme CurrentTheme { get; }

    event EventHandler? ThemeChanged;

    void ApplyTheme(AppTheme theme);
}
