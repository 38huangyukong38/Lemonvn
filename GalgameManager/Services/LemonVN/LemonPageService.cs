using GalgameManager;
using GalgameManager.Contracts.Services;
using LemonVN.ViewModels;
using LemonVN.Views;
using LemonVN.ViewModels.Anime;
using LemonVN.Views.Anime;

namespace LemonVN.Services;

public sealed class LemonPageService : IPageService
{
    private readonly Dictionary<string, Type> _pages = new()
    {
        [typeof(LemonHomeViewModel).FullName!] = typeof(LemonHomePage),
        [typeof(AnimeLibraryViewModel).FullName!] = typeof(AnimeLibraryPage),
        [typeof(LemonCategoriesViewModel).FullName!] = typeof(LemonCategoriesPage),
        [typeof(LemonRecognitionViewModel).FullName!] = typeof(LemonRecognitionPage),
        [typeof(LemonSettingsViewModel).FullName!] = typeof(LemonSettingsPage),
        [typeof(AnimeDetailViewModel).FullName!] = typeof(AnimeDetailPage),
        [typeof(AnimeEditViewModel).FullName!] = typeof(AnimeEditPage),
        [typeof(AnimeRecycleViewModel).FullName!] = typeof(AnimeRecyclePage),
        [typeof(AnimeLookupViewModel).FullName!] = typeof(AnimeLookupPage)
    };

    public Action? OnInit { get; set; }
    public bool IsAllowed(Type pageType) => _pages.ContainsValue(pageType);
    public Type GetPageType(string key) => _pages.TryGetValue(key, out Type? page)
        ? page : throw new InvalidOperationException("This page is not enabled in LemonVN.");

    public Task InitAsync()
    {
        App.MainWindow ??= new MainWindow();
        if (App.MainWindow.Content is null)
        {
            App.MainWindow.Content = App.GetService<LemonShellPage>();
            OnInit?.Invoke();
        }
        return Task.CompletedTask;
    }
}
