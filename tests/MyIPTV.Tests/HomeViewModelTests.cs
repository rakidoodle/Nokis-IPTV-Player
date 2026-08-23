using MyIPTV.App.ViewModels;
using MyIPTV.Core.Abstractions;
using MyIPTV.Core.Models;

namespace MyIPTV.Tests;

[TestClass]
public sealed class HomeViewModelTests
{
    [TestMethod]
    public async Task FavoriteCountUpdatesWhenRepositoryChanges()
    {
        FakeFavoriteRepository favorites = new();
        HomeViewModel viewModel = new(new NoOpNavigationService(), favorites);

        await favorites.SetAsync(
            new(Guid.NewGuid(), ContentKind.Movie, "movie-1", "Demo Movie", DateTimeOffset.UtcNow),
            true);

        Assert.AreEqual(1, viewModel.FavoriteCount);
    }

    private sealed class NoOpNavigationService : INavigationService
    {
        public object? CurrentViewModel => null;
        public event EventHandler? CurrentViewModelChanged;
        public void NavigateTo<TViewModel>() where TViewModel : class =>
            CurrentViewModelChanged?.Invoke(this, EventArgs.Empty);
        public void NavigateTo(Type viewModelType) =>
            CurrentViewModelChanged?.Invoke(this, EventArgs.Empty);
    }
}
