using System.Windows;
using System.Collections.ObjectModel;

namespace MyIPTV.App.Services;

public sealed class ThemeService : IThemeService
{
    private const string ThemeResourcePrefix = "Resources/Theme.";

    public AppTheme CurrentTheme { get; private set; } = AppTheme.Dark;

    public event EventHandler? ThemeChanged;

    public void ApplyTheme(AppTheme theme)
    {
        Collection<ResourceDictionary> dictionaries =
            Application.Current.Resources.MergedDictionaries;
        ResourceDictionary? currentThemeDictionary = dictionaries.FirstOrDefault(
            dictionary => dictionary.Source?.OriginalString.Contains(
                ThemeResourcePrefix,
                StringComparison.OrdinalIgnoreCase) == true);

        ResourceDictionary newThemeDictionary = new()
        {
            Source = new Uri(
                $"/MyIPTV.App;component/Resources/Theme.{theme}.xaml",
                UriKind.RelativeOrAbsolute),
        };

        if (currentThemeDictionary is null)
        {
            dictionaries.Insert(0, newThemeDictionary);
        }
        else
        {
            int index = dictionaries.IndexOf(currentThemeDictionary);
            dictionaries[index] = newThemeDictionary;
        }

        CurrentTheme = theme;
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }
}
