namespace MyIPTV.Core.Abstractions;

public interface INavigationService
{
    object? CurrentViewModel { get; }

    event EventHandler? CurrentViewModelChanged;

    void NavigateTo<TViewModel>() where TViewModel : class;

    void NavigateTo(Type viewModelType);
}
