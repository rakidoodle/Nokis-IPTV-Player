using Microsoft.Extensions.DependencyInjection;
using MyIPTV.Core.Abstractions;

namespace MyIPTV.App.Services;

public sealed class NavigationService(IServiceProvider serviceProvider) : INavigationService
{
    public object? CurrentViewModel { get; private set; }

    public event EventHandler? CurrentViewModelChanged;

    public void NavigateTo<TViewModel>() where TViewModel : class
    {
        NavigateTo(typeof(TViewModel));
    }

    public void NavigateTo(Type viewModelType)
    {
        ArgumentNullException.ThrowIfNull(viewModelType);
        CurrentViewModel = serviceProvider.GetRequiredService(viewModelType);
        CurrentViewModelChanged?.Invoke(this, EventArgs.Empty);
    }
}
