namespace MyIPTV.App.ViewModels;

public sealed record NavigationItemViewModel(
    string Label,
    string Glyph,
    Type ViewModelType);
