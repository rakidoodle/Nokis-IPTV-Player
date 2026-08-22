using Microsoft.Extensions.DependencyInjection;
using MyIPTV.Core.Abstractions;

namespace MyIPTV.App.Services;

public sealed class NavigationService(IServiceProvider serviceProvider) : INavigationService
{
    public object? CurrentViewModel { get; private set; }

    public event EventHandler? CurrentViewModelChanged;

    public void NavigateTo<TViewModel>() where TViewModel : class
    {
        CurrentViewModel = serviceProvider.GetRequiredService<TViewModel>();
        CurrentViewModelChanged?.Invoke(this, EventArgs.Empty);
    }
}
