using Windows.ApplicationModel;
using Windows.Storage;
using GalgameManager.Contracts.Services;
using GalgameManager.Helpers;
using GalgameManager.Services;
using LemonVN.Contracts.Services;
using LemonVN.ViewModels;
using LemonVN.Views;
using LemonVN.Application.Anime;
using LemonVN.Infrastructure.Anime;
using LemonVN.Infrastructure.Backup;
using LemonVN.Infrastructure.Bangumi;
using LemonVN.Infrastructure.Recognition;
using LemonVN.ViewModels.Anime;
using LemonVN.Views.Anime;
using Microsoft.Extensions.DependencyInjection;

namespace LemonVN.Services;

public static class LemonServiceRegistration
{
    // Explicit allow-list: legacy services remain in source but cannot be resolved.
    public static IServiceCollection AddLemonVnShell(this IServiceCollection services, IAppDataPathService? paths = null)
    {
        if (paths is not null) services.AddSingleton(paths);
        else services.AddSingleton<IAppDataPathService>(_ => CreateCurrentPaths());
        services.AddSingleton<IAppSettingsService, AppSettingsService>();
        services.AddSingleton<LicenseConsentService>();
        services.AddSingleton<AppDatabaseService>();
        services.AddSingleton<AppLogService>();
        services.AddSingleton<IAnimeRepository, LiteDbAnimeRepository>();
        services.AddSingleton<ICoverStorageService, CoverStorageService>();
        services.AddSingleton<AnimeCollectionService>();
        services.AddSingleton<AnimeProgressService>();
        services.AddSingleton<AnimeQueryService>();
        services.AddSingleton<AnimeBackupService>();
        services.AddSingleton(provider => BangumiHttpTransport.CreateProduction(() => provider.GetRequiredService<IAppSettingsService>().BangumiEnabled));
        services.AddSingleton<IBangumiAnimeProvider, BangumiAnimeProvider>();
        services.AddSingleton<IRemoteAnimeCoverCache, RemoteAnimeCoverCache>();
        services.AddSingleton<AnimeMetadataService>();
        services.AddSingleton<IRecognitionImageLoader, RecognitionImageLoader>();
        services.AddSingleton(_ => RecognitionHttpTransport.CreateProduction());
        services.AddSingleton<TraceMoeProvider>();
        services.AddSingleton<AnimeTraceProvider>();
        services.AddSingleton<IImageRecognitionService, ImageRecognitionService>();
        services.AddSingleton<IRecognitionUiService, RecognitionUiService>();
        services.AddSingleton<IBackupUiService, BackupUiService>();
        services.AddSingleton<IAnimeUiService, AnimeUiService>();
        services.AddSingleton<LemonThemeService>();
        services.AddSingleton<IPageService, LemonPageService>();
        services.AddSingleton<INavigationService>(provider => new NavigationService(provider.GetRequiredService<IPageService>()));
        services.AddSingleton<INavigationViewService>(provider => new NavigationViewService(
            provider.GetRequiredService<INavigationService>(), provider.GetRequiredService<IPageService>(),
            typeof(LemonSettingsViewModel).FullName!));
        services.AddSingleton<IActivationService, LemonActivationService>();
        services.AddTransient<LemonShellViewModel>();
        services.AddTransient<LemonShellPage>();
        services.AddTransient<LemonHomeViewModel>();
        services.AddTransient<LemonHomePage>();
        services.AddTransient<AnimeLibraryViewModel>();
        services.AddTransient<AnimeLibraryPage>();
        services.AddTransient<LemonCategoriesViewModel>();
        services.AddTransient<LemonCategoriesPage>();
        services.AddTransient<LemonRecognitionViewModel>();
        services.AddTransient<LemonRecognitionPage>();
        services.AddTransient<LemonSettingsViewModel>();
        services.AddTransient<BackupSettingsViewModel>();
        services.AddTransient<LemonSettingsPage>();
        services.AddTransient<AnimeDetailViewModel>();
        services.AddTransient<AnimeProgressViewModel>();
        services.AddTransient<AnimeRecycleViewModel>();
        services.AddTransient<AnimeRecyclePage>();
        services.AddTransient<AnimeDetailPage>();
        services.AddTransient<AnimeEditViewModel>();
        services.AddTransient<AnimeEditPage>();
        services.AddTransient<AnimeLookupViewModel>();
        services.AddTransient<AnimeLookupPage>();
        return services;
    }

    private static IAppDataPathService CreateCurrentPaths()
    {
        string? packageName = RuntimeHelper.IsMSIX ? Package.Current.Id.Name : null;
        if (packageName is not null && packageName != AppIdentity.PackageName)
            throw new InvalidOperationException("LemonVN must run under its own package identity.");
        // Test/development override only; never read POTATOVN_*.
        var explicitRoot = Environment.GetEnvironmentVariable("LEMONVN_LOCALDATA_PATH");
        if (!string.IsNullOrWhiteSpace(explicitRoot)) return new AppDataPathService(explicitRoot);
        if (packageName is not null)
            return AppDataPathService.ForPackage(packageName, ApplicationData.Current.LocalFolder.Path);
        return new AppDataPathService();
    }
}
