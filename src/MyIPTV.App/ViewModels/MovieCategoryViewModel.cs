namespace MyIPTV.App.ViewModels;

public sealed record MovieCategoryViewModel(string Name, int Count, string? CategoryId)
{
    public string DisplayName => $"{Name} ({Count:N0})";
}
