using System.Text.Json;
using LemonVN;
using LemonVN.Contracts.Services;
using LemonVN.Services;
using LiteDB;

namespace GalgameManager.Test.Services;

[TestFixture]
public sealed class LemonAppStorageTest
{
    private static AppDataPathService NewPaths() => new(Path.Combine(
        TestEnvironmentSetup.Root, "LemonStorage", Guid.NewGuid().ToString("N")));

    [Test]
    public void Settings_RestartPersistsTheme_AndKeepsPreviousVersion()
    {
        var paths = NewPaths();
        var settings = new AppSettingsService(paths);
        settings.Initialize();
        settings.SaveTheme(ThemePreference.Light);
        settings.SaveTheme(ThemePreference.Dark);
        var restarted = new AppSettingsService(paths);
        restarted.Initialize();
        Assert.That(restarted.Theme, Is.EqualTo(ThemePreference.Dark));
        using var backup = JsonDocument.Parse(File.ReadAllText(Path.Combine(paths.SettingsPath, "settings.backup.json")));
        Assert.That(backup.RootElement.GetProperty("Theme").GetInt32(), Is.EqualTo((int)ThemePreference.Light));
        Assert.That(Directory.GetFiles(paths.SettingsPath, "*.tmp"), Is.Empty);
    }

    [TestCase("{ corrupt")]
    [TestCase("{}")]
    [TestCase("{\"FormatVersion\":1,\"Application\":\"LemonVN.Desktop\",\"Theme\":99}")]
    [TestCase("{\"FormatVersion\":99,\"Application\":\"LemonVN.Desktop\"}")]
    [TestCase("{\"FormatVersion\":1,\"Application\":\"PotatoVN\"}")]
    public void UnreadableSettings_ArePreserved(string content)
    {
        var paths = NewPaths();
        paths.EnsureDirectories();
        var settings = new AppSettingsService(paths);
        File.WriteAllText(settings.FilePath, content);
        Assert.That(() => settings.Initialize(), Throws.Exception);
        Assert.That(File.ReadAllText(settings.FilePath), Is.EqualTo(content));
    }

    [Test]
    public void FailedSave_DoesNotChangeInMemoryTheme()
    {
        var paths = NewPaths();
        paths.EnsureDirectories();
        var settings = new AppSettingsService(paths);
        settings.Initialize();
        settings.SaveTheme(ThemePreference.Light);
        using var blocked = new FileStream(settings.FilePath, FileMode.Open, FileAccess.Read, FileShare.None);
        Assert.Throws<IOException>(() => settings.SaveTheme(ThemePreference.Dark));
        Assert.That(settings.Theme, Is.EqualTo(ThemePreference.Light));
        Assert.That(Directory.GetFiles(paths.SettingsPath, "*.tmp"), Is.Empty);
    }

    [Test]
    public void Database_CanReopen_WithoutCreatingGameCollections()
    {
        var paths = NewPaths();
        using (var database = new AppDatabaseService(paths))
        {
            database.Initialize();
            Assert.That(database.IsOpen, Is.True);
        }
        using (var restarted = new AppDatabaseService(paths)) restarted.Initialize();
        using var readOnly = new LiteDatabase(new ConnectionString { Filename = paths.DatabasePath, ReadOnly = true });
        Assert.That(readOnly.GetCollectionNames(), Is.EquivalentTo(new[] { "app_info" }));
        Assert.That(readOnly.GetCollection("app_info").FindById("identity")["application"].AsString,
            Is.EqualTo(AppIdentity.PackageName));
    }

    [Test]
    public void UnownedDatabase_IsRejected_WithoutModifyingIt()
    {
        var paths = NewPaths();
        paths.EnsureDirectories();
        using (var unknown = new LiteDatabase(paths.DatabasePath))
            unknown.GetCollection("sentinel").Insert(new BsonDocument { ["_id"] = "keep", ["value"] = "unchanged" });
        byte[] before = File.ReadAllBytes(paths.DatabasePath);
        using var appDatabase = new AppDatabaseService(paths);
        Assert.Throws<InvalidDataException>(() => appDatabase.Initialize());
        Assert.That(File.ReadAllBytes(paths.DatabasePath), Is.EqualTo(before));
    }

    [Test]
    public void StorageInitialization_LeavesSiblingPotatoVnFixtureUntouched()
    {
        var fixtureRoot = Path.Combine(TestEnvironmentSetup.Root, "Coexist", Guid.NewGuid().ToString("N"));
        var potatoRoot = Path.Combine(fixtureRoot, "PotatoVN");
        Directory.CreateDirectory(potatoRoot);
        var sentinel = Path.Combine(potatoRoot, "pvn_data.db");
        File.WriteAllText(sentinel, "synthetic original data");
        var paths = new AppDataPathService(Path.Combine(fixtureRoot, "LemonVN"));
        paths.EnsureDirectories();
        using (var database = new AppDatabaseService(paths)) database.Initialize();
        var settings = new AppSettingsService(paths);
        settings.Initialize();
        settings.SaveTheme(ThemePreference.Dark);
        using (var log = new AppLogService(paths))
        {
            log.Initialize();
            log.Information("Local test only");
        }
        Assert.That(File.ReadAllText(sentinel), Is.EqualTo("synthetic original data"));
        Assert.That(Directory.GetFiles(potatoRoot), Has.Length.EqualTo(1));
        Assert.That(Directory.GetFiles(paths.LogsPath), Is.Not.Empty);
        Assert.That(Directory.Exists(paths.UserDataPath), Is.True);
        Assert.That(Directory.Exists(paths.ExportsPath), Is.True);
    }

    [Test]
    public void PackagedRoot_IsInsideOwnPackage_AndRejectsUpstreamIdentity()
    {
        var packageFolder = Path.Combine(TestEnvironmentSetup.Root, "Package", Guid.NewGuid().ToString("N"));
        var paths = AppDataPathService.ForPackage(AppIdentity.PackageName, packageFolder);
        Assert.That(paths.RootPath, Is.EqualTo(Path.Combine(packageFolder, "LemonVN")));
        Assert.That(() => AppDataPathService.ForPackage("37126GoldenPotato137.PotatoVN", packageFolder),
            Throws.TypeOf<InvalidOperationException>());
        Assert.That(Directory.Exists(packageFolder), Is.False);
    }
}
