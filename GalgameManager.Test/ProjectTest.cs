// LemonVN modification from the fixed PotatoVN baseline; see LICENSE, NOTICE and UPSTREAM_PROVENANCE.json.
using System.Text.Json;
using System.Xml.Linq;
using GalgameManager.Helpers;

namespace GalgameManager.Test;

[TestFixture]
public class ProjectTest
{
    [Test]
    public void Version_ShouldMatch_PackageAppxmanifest()
    {
        var manifestVersion = ReadPackageAppxManifestIdentityVersion();

        var runtimeVersion = RuntimeHelper.GetVersion();
        Assert.That(runtimeVersion, Is.EqualTo(manifestVersion));

        var assemblyVersion = typeof(RuntimeHelper).Assembly.GetName().Version?.ToString();
        Assert.That(assemblyVersion, Is.Not.Null);
        Assert.That(assemblyVersion, Is.EqualTo(manifestVersion));
    }

    [Test]
    public void UpstreamAndModificationNotices_AreCopiedUnchangedToClientOutput()
    {
        string workspace = Directory.GetParent(Path.GetDirectoryName(FindPackageAppxManifestPath())!)!.FullName;
        string configuration = AppContext.BaseDirectory.Contains("\\Release\\", StringComparison.OrdinalIgnoreCase) ? "Release" : "Debug";
        string output = Path.Combine(workspace, "GalgameManager", "bin", "x64", configuration,
            "net8.0-windows10.0.22621.0", "win-x64");
        foreach (string name in new[] { "LICENSE", "NOTICE", "THIRD_PARTY_NOTICES.md" })
        {
            string source = Path.Combine(workspace, name), copied = Path.Combine(output, name);
            Assert.That(File.Exists(copied), Is.True, "Missing client notice: " + name);
            Assert.That(File.ReadAllBytes(copied), Is.EqualTo(File.ReadAllBytes(source)), "Notice was changed or truncated: " + name);
        }
        string notice = File.ReadAllText(Path.Combine(output, "NOTICE"));
        Assert.That(notice, Does.Contain("GoldenPotato137").And.Contain("2adef493ea0d82d27d4037e91507e558c567305d"));
    }

    [Test]
    public void ClientBuild_HasNoAutoMapperReferenceOrRuntimePayload()
    {
        string project = Path.GetDirectoryName(FindPackageAppxManifestPath())!;
        string configuration = AppContext.BaseDirectory.Contains("\\Release\\", StringComparison.OrdinalIgnoreCase) ? "Release" : "Debug";
        using JsonDocument assets = JsonDocument.Parse(File.ReadAllText(Path.Combine(project, "obj", "project.assets.json")));
        Assert.Multiple(() =>
        {
            Assert.That(typeof(RuntimeHelper).Assembly.GetReferencedAssemblies().Select(a => a.Name), Does.Not.Contain("AutoMapper"));
            Assert.That(assets.RootElement.GetProperty("libraries").EnumerateObject().Any(p => p.Name.StartsWith("AutoMapper/", StringComparison.OrdinalIgnoreCase)), Is.False);
            Assert.That(File.Exists(Path.Combine(project, "bin", "x64", configuration,
                "net8.0-windows10.0.22621.0", "win-x64", "AutoMapper.dll")), Is.False);
        });
    }

    private static string ReadPackageAppxManifestIdentityVersion()
    {
        var manifestPath = FindPackageAppxManifestPath();

        var doc = XDocument.Load(manifestPath);
        var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

        var identity = doc.Root?.Elements(ns + "Identity").FirstOrDefault();
        var version = identity?.Attribute("Version")?.Value;

        if (string.IsNullOrWhiteSpace(version))
        {
            Assert.Fail($"未能从 {manifestPath} 读取 Identity Version。请检查 Package.appxmanifest 是否包含 <Identity Version=.../>。");
        }

        return version!;
    }

    private static string FindPackageAppxManifestPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        for (var i = 0; i < 12 && dir != null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "GalgameManager", "Package.appxmanifest");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        Assert.Fail($"未找到 GalgameManager/Package.appxmanifest。当前测试目录: {AppContext.BaseDirectory}");
        return string.Empty;
    }
}
