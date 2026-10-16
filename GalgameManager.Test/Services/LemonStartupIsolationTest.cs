using System.Xml.Linq;
using GalgameManager.Contracts.Services;
using GalgameManager.Services;
using LemonVN;
using LemonVN.Services;
using LemonVN.ViewModels;
using LemonVN.Views;
using Microsoft.Extensions.DependencyInjection;

namespace GalgameManager.Test.Services;

[TestFixture]
public sealed class LemonStartupIsolationTest
{
    [Test]
    public void ServiceGraph_ResolvesStartup_WithoutOpeningStorageOrRegisteringLegacyServices()
    {
        var root = Path.Combine(TestEnvironmentSetup.Root, "StartupGraph", Guid.NewGuid().ToString("N"));
        var services = new ServiceCollection().AddLemonVnShell(new AppDataPathService(root));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
            { ValidateOnBuild = true, ValidateScopes = true });
        Assert.That(provider.GetRequiredService<IActivationService>(), Is.TypeOf<LemonActivationService>());
        Assert.That(provider.GetRequiredService<IPageService>(), Is.TypeOf<LemonPageService>());
        foreach (Type legacy in new[]
                 {
                     typeof(ILocalSettingsService), typeof(IGalgameCollectionService),
                     typeof(IGalgameSourceCollectionService), typeof(IGameLaunchService), typeof(IBgTaskService),
                     typeof(IPvnService), typeof(IPluginService), typeof(IUpdateService), typeof(IAppCenterService),
                     typeof(IBgmOAuthService), typeof(ISteamService), typeof(IAutoExportService), typeof(IInfoService)
                 })
            Assert.That(provider.GetService(legacy), Is.Null, $"Legacy service {legacy.Name} must remain disabled.");
        Assert.That(services.Any(descriptor => descriptor.ServiceType == typeof(HttpClient)), Is.False);
        Assert.That(Directory.Exists(root), Is.False);
    }

    [Test]
    public void Navigation_RejectsGamePages_BeforeAnyViewOrServiceIsCreated()
    {
        var pages = new LemonPageService();
        var navigation = new NavigationService(pages);
        Assert.That(pages.GetPageType(typeof(LemonHomeViewModel).FullName!), Is.EqualTo(typeof(LemonHomePage)));
        Assert.That(pages.GetPageType(typeof(LemonSettingsViewModel).FullName!), Is.EqualTo(typeof(LemonSettingsPage)));
        Assert.Throws<InvalidOperationException>(() => navigation.NavigateTo("GalgameManager.ViewModels.HomeViewModel"));
        Assert.Throws<InvalidOperationException>(() => navigation.NavigateTo(typeof(GalgameManager.Views.HomePage)));
        Assert.That(pages.IsAllowed(typeof(GalgameManager.Views.PluginPage)), Is.False);
    }

    [Test]
    public void PackageAndExecutableIdentity_AreIndependent_WithoutUpstreamActivationExtensions()
    {
        var root = RepositoryRoot();
        XDocument manifest = XDocument.Load(Path.Combine(root, "GalgameManager", "Package.appxmanifest"));
        XNamespace ns = manifest.Root!.Name.Namespace;
        XElement identity = manifest.Root.Element(ns + "Identity")!;
        Assert.That(identity.Attribute("Name")!.Value, Is.EqualTo(AppIdentity.PackageName));
        Assert.That(identity.Attribute("Publisher")!.Value, Is.EqualTo(AppIdentity.Publisher));
        Assert.That(manifest.Root.Element(ns + "Properties")!.Element(ns + "DisplayName")!.Value,
            Is.EqualTo(AppIdentity.DisplayName));
        Assert.That(manifest.Descendants(ns + "Application").Single().Attribute("Id")!.Value,
            Is.EqualTo(AppIdentity.ApplicationId));
        Assert.That(manifest.Descendants().Where(x => x.Name.LocalName == "Extensions"), Is.Empty);
        XDocument project = XDocument.Load(Path.Combine(root, "GalgameManager", "GalgameManager.csproj"));
        Assert.That(project.Descendants("AssemblyName").Single().Value, Is.EqualTo("LemonVN"));
        Assert.That(project.Descendants("GenerateTemporaryStoreCertificate").Single().Value, Is.EqualTo("False"));
        XDocument executableManifest = XDocument.Load(Path.Combine(root, "GalgameManager", "app.manifest"));
        Assert.That(executableManifest.Descendants().Single(x => x.Name.LocalName == "requestedExecutionLevel")
            .Attribute("level")!.Value, Is.EqualTo("asInvoker"));
    }

    private static string RepositoryRoot()
    {
        for (DirectoryInfo? folder = new(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
            if (File.Exists(Path.Combine(folder.FullName, "GalgameManager.sln"))) return folder.FullName;
        throw new DirectoryNotFoundException("Cannot locate the repository fixture.");
    }
}
