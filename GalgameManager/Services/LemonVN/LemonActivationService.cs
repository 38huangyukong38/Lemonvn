using GalgameManager;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using LemonVN.Contracts.Services;
using LemonVN.ViewModels;

namespace LemonVN.Services;

public sealed class LemonActivationService(IAppDataPathService paths, IAppSettingsService settings,
    AppDatabaseService database, AppLogService log, IPageService pages,
    INavigationService navigation, LemonThemeService theme) : IActivationService
{
    public async Task LaunchedAsync(object activationArgs)
    {
        paths.EnsureDirectories();
        log.Initialize();
        log.Information("LemonVN startup: local storage, navigation and theme only.");
        settings.Initialize();
        database.Initialize();
        await pages.InitAsync();
        theme.Apply();
        if (!navigation.NavigateTo(typeof(LemonHomeViewModel).FullName!))
            throw new InvalidOperationException("The LemonVN home page could not be opened.");
        App.Status = WindowMode.Normal;
        App.MainWindow!.Activate();
        log.Information("LemonVN main window ready; no game, account, update, telemetry or plugin services registered.");
    }

    public Task HandleActivationAsync(object activationArgs)
    {
        // No game launch, legacy import, OAuth or protocol handlers.
        App.MainWindow?.Activate();
        return Task.CompletedTask;
    }
}
