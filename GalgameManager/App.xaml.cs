// LemonVN modification from the fixed PotatoVN baseline; see LICENSE, NOTICE and UPSTREAM_PROVENANCE.json.
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using H.NotifyIcon;
using LemonVN;
using LemonVN.Helpers;
using LemonVN.Services;
using LemonVN.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using UnhandledExceptionEventArgs = Microsoft.UI.Xaml.UnhandledExceptionEventArgs;

namespace GalgameManager;

// LemonVN adaptation: preserve the TemplateStudio/WinUI application contract,
// while retaining legacy business modules only as unregistered reference source.
public partial class App : Application
{
    public IHost Host { get; }
    public static WindowEx? MainWindow { get; set; }
    public static TaskbarIcon? SystemTray { get; set; }
    public static UIElement? AppTitlebar { get; set; }
    public static WindowMode Status = WindowMode.Booting;
    public static event Action? OnAppClosing;
    public static DispatcherQueue DispatcherQueue { get; } = DispatcherQueue.GetForCurrentThread();
    private bool _closed;
    private bool _businessInitializationStarted;
    private bool _consentWindowClosed;

    public App()
    {
        LemonLocalization.Initialize();
        InitializeComponent();
        // No default configuration, plugin XAML host, game discovery, account
        // handlers, telemetry or background-task restoration.
        Host = new HostBuilder().UseContentRoot(AppContext.BaseDirectory)
            .ConfigureServices((_, services) => services.AddLemonVnShell()).Build();
        UnhandledException += App_UnhandledException;
    }

    public static T GetService<T>() where T : class =>
        (Current as App)?.Host.Services.GetService<T>()
        ?? throw new InvalidOperationException($"{typeof(T).Name} is not enabled in LemonVN.");

    public static T GetResource<T>(string key) => Current.Resources[key] is T resource
        ? resource : throw new ArgumentException($"Missing resource: {key}");

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);
        try
        {
            // The separate ZIP uninstall entry opens no collection, settings,
            // logging or network service, and does not join the main instance.
            if (BootstrapProgram.UninstallRequested)
            {
                MainWindow = new MainWindow { MinWidth = 640, MinHeight = 480, Width = 760, Height = 560 };
                MainWindow.Content = new UninstallPage(GetService<LemonVN.Contracts.Services.IAppDataPathService>());
                MainWindow.Closed += (_, _) => Shutdown();
                MainWindow.Activate();
                return;
            }
            AppInstance registration = AppInstance.FindOrRegisterForKey(AppIdentity.SingleInstanceKey);
            if (!registration.IsCurrent)
            {
                await registration.RedirectActivationToAsync(AppInstance.GetCurrent().GetActivatedEventArgs());
                Shutdown();
                Exit();
                return;
            }
            registration.Activated += OnActivated;
#pragma warning disable CS0618
            WinApp.Base.Helpers.UiThreadInvokeHelper.Init(DispatcherQueue);
#pragma warning restore CS0618
            var terms = LicenseTerms.Load(AppContext.BaseDirectory);
            LicenseConsentPage? consentPage = null;
            bool accepted = await GetService<LicenseConsentService>().RunAsync(terms, async () =>
            {
                // Only read local theme preferences; do not resolve business activation/storage yet.
                var settings = GetService<LemonVN.Contracts.Services.IAppSettingsService>();
                settings.Initialize();
                MainWindow ??= new MainWindow();
                MainWindow.MinWidth = 640;
                MainWindow.MinHeight = 480;
                MainWindow.Width = 780;
                MainWindow.Height = 580;
                consentPage = new LicenseConsentPage(terms)
                {
                    RequestedTheme = settings.Theme switch
                    {
                        LemonVN.Contracts.Services.ThemePreference.Light => ElementTheme.Light,
                        LemonVN.Contracts.Services.ThemePreference.Dark => ElementTheme.Dark,
                        _ => ElementTheme.Default
                    }
                };
                MainWindow.Content = consentPage;
                void ClosedWhileAwaitingConsent(object sender, WindowEventArgs e)
                {
                    _consentWindowClosed = true;
                    consentPage.Cancel();
                }
                MainWindow.Closed += ClosedWhileAwaitingConsent;
                MainWindow.Activate();
                try { return await consentPage.Decision && !_consentWindowClosed; }
                finally { MainWindow.Closed -= ClosedWhileAwaitingConsent; }
            }, async () =>
            {
                if (consentPage is not null)
                {
                    MainWindow!.Content = null;
                    MainWindow.MinWidth = 960;
                    MainWindow.MinHeight = 600;
                    MainWindow.Width = 1280;
                    MainWindow.Height = 800;
                }
                _businessInitializationStarted = true;
                await GetService<IActivationService>().LaunchedAsync(AppInstance.GetCurrent().GetActivatedEventArgs());
            });
            if (!accepted)
            {
                if (!_consentWindowClosed) MainWindow?.Close();
                Shutdown();
                Exit();
                return;
            }
            MainWindow!.Closed += (_, _) => Shutdown();
        }
        catch (Exception error)
        {
            TryLog(error, "LemonVN startup failed; data was not reset or rebuilt.");
            BootstrapProgram.ReportStartupFailure(error);
            if (!_consentWindowClosed) MainWindow?.Close();
            Shutdown();
            Exit();
        }
    }

    private void OnActivated(object? sender, AppActivationArguments args)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_closed || MainWindow is null) return;
            MainWindow.Restore();
            MainWindow.Activate();
            MainWindow.BringToFront();
        });
    }

    private void App_UnhandledException(object sender, UnhandledExceptionEventArgs args)
    {
        args.Handled = true;
        try
        {
            TryLog(args.Exception, "Unhandled UI error.");
            if (MainWindow?.Content is LemonShellPage shell) shell.ViewModel.ShowError(args.Message);
        }
        catch
        {
            // Failure reporting must never throw, recurse, restart, or migrate data.
        }
    }

    private void TryLog(Exception error, string message)
    {
        if (!_businessInitializationStarted) return;
        try { Host.Services.GetService<AppLogService>()?.Error(error, message); }
        catch { /* Reporting is best-effort. */ }
    }

    private void Shutdown()
    {
        if (_closed) return;
        _closed = true;
        Status = WindowMode.Close;
        try
        {
            if (_businessInitializationStarted)
                Host.Services.GetService<AppLogService>()?.Information("LemonVN shutdown: closing local database and logs.");
            OnAppClosing?.Invoke();
        }
        finally
        {
            Host.Dispose();
        }
    }

    // Compatibility with compiled reference pages; there is no tray restart,
    // task restoration, game operation, or sync side effect in this host.
    public static void SetWindowMode(WindowMode mode)
    {
        if (mode == WindowMode.Close)
        {
            MainWindow?.Close();
            (Current as App)?.Shutdown();
            (Current as App)?.Exit();
        }
        else if (mode is WindowMode.Minimize or WindowMode.SystemTray) MainWindow?.Minimize();
        else if (mode == WindowMode.Normal)
        {
            MainWindow?.Activate();
            MainWindow?.BringToFront();
        }
    }

    public static bool IsStoreVersion() => false; // No PotatoVN store/update channel.
}
