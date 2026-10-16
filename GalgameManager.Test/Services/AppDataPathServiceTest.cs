using LemonVN.Services;

namespace GalgameManager.Test.Services;

[TestFixture]
public sealed class AppDataPathServiceTest
{
    [Test]
    public void DefaultRoot_IsLemonVnLocalAppData_AndIgnoresPotatoVnOverride()
    {
        var service = new AppDataPathService();

        Assert.That(service.RootPath, Is.EqualTo(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LemonVN")));
        // TestEnvironmentSetup sets POTATOVN_LOCALDATA_PATH to its own fixture directory.
        Assert.That(service.RootPath, Is.Not.EqualTo(TestEnvironmentSetup.LocalDataPath));
    }

    [Test]
    public void Constructor_DoesNotCreateDataOrOpenDatabase()
    {
        var root = NewTestRoot();
        var service = new AppDataPathService(root);

        Assert.That(Directory.Exists(service.RootPath), Is.False);
        Assert.That(File.Exists(service.DatabasePath), Is.False);
    }

    [Test]
    public void EveryDataDirectory_IsInsideTheDedicatedRoot()
    {
        var root = NewTestRoot();
        var service = new AppDataPathService(root);

        foreach (var directory in new[]
                 { service.SettingsPath, service.CoversPath, service.CachePath, service.BackupsPath, service.LogsPath, service.TempPath })
            Assert.That(Path.GetDirectoryName(directory), Is.EqualTo(root));
        Assert.That(service.DatabasePath, Is.EqualTo(Path.Combine(root, "lemonvn.db")));
    }

    [Test]
    public void EnsureDirectories_CanRepeat_WithoutChangingExistingData()
    {
        var root = NewTestRoot();
        var service = new AppDataPathService(root);
        service.EnsureDirectories();
        var marker = Path.Combine(service.CoversPath, "existing-cover.txt");
        File.WriteAllText(marker, "preserve this fixture");

        service.EnsureDirectories();

        foreach (var directory in new[]
                 { root, service.SettingsPath, service.CoversPath, service.CachePath, service.BackupsPath, service.LogsPath, service.TempPath })
            Assert.That(Directory.Exists(directory), Is.True);
        Assert.That(File.ReadAllText(marker), Is.EqualTo("preserve this fixture"));
        Assert.That(File.Exists(service.DatabasePath), Is.False);
    }

    [TestCase("")]
    [TestCase("relative-data")]
    [TestCase("../PotatoVN")]
    [TestCase("C:\\")]
    public void UnsafeRoot_IsRejected(string root)
    {
        Assert.Throws<ArgumentException>(() => new AppDataPathService(root));
    }

    private static string NewTestRoot() =>
        Path.Combine(TestEnvironmentSetup.Root, "LemonVN", Guid.NewGuid().ToString("N"));
}
