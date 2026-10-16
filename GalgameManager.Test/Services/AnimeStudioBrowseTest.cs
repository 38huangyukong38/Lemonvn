using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using LemonVN.Application.Anime;
using LemonVN.Contracts.Services;
using LemonVN.Domain.Anime;
using LemonVN.Infrastructure.Anime;
using LemonVN.Infrastructure.Backup;
using LemonVN.Infrastructure.Bangumi;
using LemonVN.Services;
using Moq;
using GalgameManager.Contracts.Services;
using LemonVN.ViewModels;
using LemonVN.ViewModels.Anime;

namespace GalgameManager.Test.Services;

[TestFixture]
public sealed class AnimeStudioBrowseTest
{
    private sealed class Store : IDisposable
    {
        public AppDataPathService Paths { get; }
        public AppDatabaseService Db { get; }
        public LiteDbAnimeRepository Repo { get; }
        public CoverStorageService Covers { get; }
        public AnimeCollectionService Collection { get; }
        public AnimeBackupService Backup { get; }
        public Store(string root)
        {
            Paths = new(root); Db = new(Paths); Db.Initialize(); Repo = new(Db); Covers = new(Paths);
            var log = new AppLogService(Paths);
            Collection = new(Repo, Covers, log); Backup = new(Repo, Collection, Covers, Paths, log);
        }
        public void Dispose() => Db.Dispose();
    }

    private static string Root() => Path.Combine(TestEnvironmentSetup.Root, "M55B", Guid.NewGuid().ToString("N"));
    private static AnimeEntry Entry(string title, params string[] studios) => new()
    {
        Metadata = new() { Title = title, AnimationStudios = studios.ToList(), TotalEpisodes = 12 },
        Record = new() { WatchStatus = WatchStatus.Watching, PersonalRating = 9, Tags = ["Music"], Notes = "preserve personal note",
            Episodes = [new() { EpisodeNumber = 2, IsWatched = true, WatchedAtUtc = new(2025, 1, 2, 10, 0, 0, DateTimeKind.Utc), Notes = "episode note" }] }
    };
    private static string Personal(AnimeEntry entry) => JsonSerializer.Serialize(entry.Record);

    [Test]
    public void StudioNames_TrimDeduplicateAndPreserveCompanyPunctuation()
    {
        Assert.That(AnimationStudioRules.Parse("  Studio A\r\nStudio, Inc.\nSTUDIO A\n\n"),
            Is.EqualTo(new[] { "Studio A", "Studio, Inc." }));
    }

    [TestCase("long")]
    [TestCase("control")]
    [TestCase("many")]
    public void InvalidStudioInput_IsRejected(string kind)
    {
        string[] names = kind == "long" ? [new('a', 129)] : kind == "control" ? ["Studio\tA"]
            : Enumerable.Range(0, 33).Select(i => "Studio " + i).ToArray();
        Assert.Throws<AnimeValidationException>(() => AnimationStudioRules.Normalize(names));
    }

    [Test]
    public void Provider_UsesOnlyExplicitAnimationProductionAndHandlesMultipleValues()
    {
        byte[] json = Encoding.UTF8.GetBytes("""
            {"id":123,"type":2,"name":"Anime","name_cn":"","eps":12,"infobox":[
              {"key":"製作","value":"Publisher"},{"key":"音乐制作","value":"Label"},
              {"key":"OP・ED 动画制作","value":"Opening Studio"},
              {"key":"动画制作","value":[{"v":" Studio A "},{"v":"Studio B"},{"v":"STUDIO A"}]}]}
            """);
        Assert.That(BangumiAnimeProvider.ParseDetails(json).AnimationStudios, Is.EqualTo(new[] { "Studio A", "Studio B" }));
    }

    [TestCase("null")]
    [TestCase("[]")]
    [TestCase("[{\"key\":\"製作\",\"value\":\"Committee\"}]")]
    public void Provider_MissingStudiosAreNotInferred(string infobox)
    {
        byte[] json = Encoding.UTF8.GetBytes("{\"id\":123,\"type\":2,\"name\":\"Anime\",\"infobox\":" + infobox + "}");
        Assert.That(BangumiAnimeProvider.ParseDetails(json).AnimationStudios, Is.Empty);
    }

    [Test]
    public void Categories_CountMultiStudioMembershipWithoutDuplicatingEntries()
    {
        AnimeEntry one = Entry("one", "A", "B"), two = Entry("two", "a"), missing = Entry("missing"), literal = Entry("literal", "未设置");
        var query = new AnimeQueryService();
        var all = new[] { one, two, missing, literal };
        var groups = query.GetStudioGroups(all);
        Assert.That(groups.Single(g => g.Studio == "A").Count, Is.EqualTo(2));
        Assert.That(groups.Single(g => g.Studio == "B").Count, Is.EqualTo(1));
        Assert.That(groups.Single(g => g.Target.UnassignedStudio).Count, Is.EqualTo(1));
        Assert.That(query.Apply(all, new(AnimationStudio: "a")).Select(e => e.Id), Is.EquivalentTo(new[] { one.Id, two.Id }));
        Assert.That(query.Apply(all, new(UnassignedStudio: true)).Single().Id, Is.EqualTo(missing.Id));
        Assert.That(groups.Single(g => g.Studio == "未设置").Target.UnassignedStudio, Is.False);
    }

    [Test]
    public void Recent_UsesWatchedEpisodeTimeAndNeverMetadataModificationTime()
    {
        AnimeEntry old = Entry("old"), recent = Entry("recent"), unknown = Entry("no date"), undone = Entry("undone");
        old.UpdatedAtUtc = DateTime.UtcNow.AddDays(2);
        recent.Record.Episodes[0].WatchedAtUtc = new(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        unknown.Record.Episodes[0].WatchedAtUtc = null;
        undone.Record.Episodes[0].IsWatched = false;
        var result = new AnimeQueryService().Apply([old, recent, unknown, undone],
            new(Sort: AnimeSort.LastWatched, OnlyWithWatchTime: true));
        Assert.That(result.Select(e => e.Id), Is.EqualTo(new[] { recent.Id, old.Id }));
    }

    [Test]
    public async Task LegacyBson_OpensWithoutRewriteAndStudioEditsSurviveRestart()
    {
        string root = Root();
        Guid id;
        string personal;
        using (var store = new Store(root))
        {
            AnimeEntry saved = await store.Collection.SaveAsync(Entry("legacy"), true);
            id = saved.Id;
            store.Db.Write(db =>
            {
                var row = db.GetCollection("anime").FindById(id)!;
                row["Metadata"].AsDocument.Remove("AnimationStudios");
                return db.GetCollection("anime").Update(row);
            });
            AnimeEntry before = (await store.Collection.GetByIdAsync(id))!;
            Assert.That(before.Metadata.AnimationStudios, Is.Empty);
            Assert.That(store.Db.Read(db => db.GetCollection("anime").FindById(id)!["Metadata"].AsDocument.ContainsKey("AnimationStudios")), Is.False);
            personal = Personal(before);
            before.Metadata.AnimationStudios = ["A", "B"];
            await store.Collection.SaveAsync(before, false);
        }
        using var reopened = new Store(root);
        AnimeEntry after = (await reopened.Collection.GetByIdAsync(id))!;
        Assert.That(after.SchemaVersion, Is.EqualTo(1));
        Assert.That(after.Metadata.AnimationStudios, Is.EqualTo(new[] { "A", "B" }));
        Assert.That(after.Metadata.UserOverrides, Does.Contain("AnimationStudios"));
        Assert.That(Personal(after), Is.EqualTo(personal));
    }

    [TestCase(true, false)]
    [TestCase(false, false)]
    [TestCase(true, true)]
    [TestCase(false, true)]
    public async Task ProviderRefresh_ProtectsManualStudiosAndPreservesFullRecord(bool protectedField, bool missingRemote)
    {
        using var store = new Store(Root());
        AnimeEntry saved = await store.Collection.SaveAsync(Entry("local", "Local Studio"), true);
        if (!protectedField)
        {
            saved.Metadata.UserOverrides.Remove("AnimationStudios");
            saved = await store.Repo.UpdateAsync(saved);
        }
        string personal = Personal((await store.Collection.GetByIdAsync(saved.Id))!);
        var provider = new Mock<IBangumiAnimeProvider>();
        provider.Setup(p => p.GetAnimeDetailsAsync("123", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BangumiAnime("123", "remote", null, null, null, 10, null, null) { AnimationStudios = missingRemote ? [] : ["Remote Studio"] });
        var cache = new Mock<IRemoteAnimeCoverCache>();
        cache.Setup(c => c.GetBytesAsync(null, It.IsAny<CancellationToken>(), true)).ReturnsAsync((byte[]?)null);
        var metadata = new AnimeMetadataService(provider.Object, cache.Object, store.Collection, store.Covers);
        var preview = await metadata.PrepareAsync("123", saved.Id, true, default);
        Assert.That(preview.Changes.Single(c => c.Field == "AnimationStudios").Kind,
            Is.EqualTo(protectedField ? MetadataChangeKind.Protected : missingRemote ? MetadataChangeKind.Missing : MetadataChangeKind.Update));
        await metadata.CommitAsync(preview, default);
        AnimeEntry after = (await store.Collection.GetByIdAsync(saved.Id))!;
        Assert.That(after.Metadata.AnimationStudios, Is.EqualTo(new[] { protectedField || missingRemote ? "Local Studio" : "Remote Studio" }));
        Assert.That(Personal(after), Is.EqualTo(personal));
    }

    [Test]
    public async Task BackupV2_TransfersStudiosAndFullPersonalRecordAcrossRoots()
    {
        using var source = new Store(Root()); using var target = new Store(Root());
        AnimeEntry saved = await source.Collection.SaveAsync(Entry("portable", "Studio A", "Studio B"), true);
        string personal = Personal((await source.Collection.GetByIdAsync(saved.Id))!);
        string file = Path.Combine(source.Paths.RootPath, "backup.zip");
        await source.Backup.ExportAsync(file);
        using (var archive = ZipFile.OpenRead(file))
        using (Stream manifest = archive.GetEntry("manifest.json")!.Open())
        using (JsonDocument document = await JsonDocument.ParseAsync(manifest))
            Assert.That(document.RootElement.GetProperty("exportFormatVersion").GetInt32(), Is.EqualTo(2));
        using var prepared = await target.Backup.PrepareAsync(file);
        await target.Backup.RestoreAsync(prepared);
        AnimeEntry after = (await target.Collection.GetByIdAsync(saved.Id))!;
        Assert.That(after.Metadata.AnimationStudios, Is.EqualTo(new[] { "Studio A", "Studio B" }));
        Assert.That(Personal(after), Is.EqualTo(personal));
    }

    [Test]
    public async Task CancelledRestore_PreservesExistingStudiosAndPersonalRecords()
    {
        using var source = new Store(Root()); using var target = new Store(Root());
        await source.Collection.SaveAsync(Entry("incoming", "Incoming"), true);
        AnimeEntry original = await target.Collection.SaveAsync(Entry("existing", "Existing"), true);
        string before = JsonSerializer.Serialize(await target.Collection.GetByIdAsync(original.Id));
        string file = Path.Combine(source.Paths.RootPath, "cancel.zip");
        await source.Backup.ExportAsync(file);
        using var prepared = await target.Backup.PrepareAsync(file);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.ThrowsAsync<OperationCanceledException>(() => target.Backup.RestoreAsync(prepared, cancellation.Token));
        Assert.That(JsonSerializer.Serialize(await target.Collection.GetByIdAsync(original.Id)), Is.EqualTo(before));
    }

    [Test]
    public async Task ClearingStudiosProtectsTheEmptyValue_AndPersonalOnlyEditDoesNotAddProtection()
    {
        using var store = new Store(Root());
        AnimeEntry saved = await store.Collection.SaveAsync(Entry("local", "Studio A"), true);
        saved.Metadata.UserOverrides.Remove("AnimationStudios"); saved = await store.Repo.UpdateAsync(saved);
        saved.Record.Notes = "edited note"; saved = await store.Collection.SaveAsync(saved, false);
        Assert.That(saved.Metadata.UserOverrides, Does.Not.Contain("AnimationStudios"));
        saved.Metadata.AnimationStudios = []; saved = await store.Collection.SaveAsync(saved, false);
        Assert.That(saved.Metadata.UserOverrides, Does.Contain("AnimationStudios"));
        Assert.That(saved.Metadata.AnimationStudios, Is.Empty);
    }

    [Test]
    public async Task HomeAndCategories_ProjectCountsBoundPreviewsAndNavigateToMatchingLists()
    {
        string root = Root();
        using var store = new Store(root);
        var rows = Enumerable.Range(0, 15).Select(i =>
        {
            AnimeEntry entry = Entry("W" + i.ToString("D2"), "A");
            entry.Record.Episodes[0].WatchedAtUtc = DateTime.UtcNow.AddMinutes(-i);
            return entry;
        }).ToList();
        AnimeEntry completed = Entry("completed", "B"); completed.Record.WatchStatus = WatchStatus.Completed;
        AnimeEntry planned = Entry("planned"); planned.Record.WatchStatus = WatchStatus.Planned; planned.Record.Episodes = [];
        AnimeEntry held = Entry("held", "A", "B"); held.Record.WatchStatus = WatchStatus.OnHold; held.Record.Episodes = [];
        rows.AddRange([completed, planned, held]);
        var repo = new Mock<IAnimeRepository>(); repo.Setup(r => r.GetAllAsync()).ReturnsAsync(rows);
        using var log = new AppLogService(store.Paths);
        var collection = new AnimeCollectionService(repo.Object, store.Covers, log);
        var navigation = new Mock<INavigationService>();
        static string Text(string key, object[] args) => key + string.Join(",", args);
        var home = new LemonHomeViewModel(navigation.Object, collection, new(), store.Covers, log, Text);
        await home.ActivateAsync();
        Assert.That(home.Recent.Count, Is.EqualTo(16));
        Assert.That(home.Recent.Items, Has.Count.EqualTo(12));
        Assert.That(home.Recent.Items[0].Id, Is.EqualTo(rows[0].Id));
        Assert.That(home.Watching.Count, Is.EqualTo(15));
        Assert.That(home.Completed.Count, Is.EqualTo(1)); Assert.That(home.Planned.Count, Is.EqualTo(1));
        home.Recent.MoreCommand.Execute(null);
        navigation.Verify(n => n.NavigateTo(typeof(AnimeLibraryViewModel).FullName!, It.Is<AnimeLibraryRequest>(r => r.Recent), false, ""));
        var categories = new LemonCategoriesViewModel(navigation.Object, collection, new(), store.Covers, log, Text);
        await categories.ActivateAsync();
        Assert.That(categories.StatusGroups.Select(g => g.Count), Is.EqualTo(new[] { 1, 15, 1, 1, 0 }));
        Assert.That(categories.StudioGroups.Single(g => g.Title == "A").Count, Is.EqualTo(16));
        Assert.That(categories.StudioGroups.Single(g => g.Target.UnassignedStudio).Count, Is.EqualTo(1));
        categories.StudioGroups.Single(g => g.Title == "A").OpenCommand.Execute(null);
        navigation.Verify(n => n.NavigateTo(typeof(AnimeLibraryViewModel).FullName!, It.Is<AnimeLibraryRequest>(r => r.AnimationStudio == "A"), false, ""));
    }

    [Test]
    public async Task LibraryRequest_ClearsOtherFiltersAndRetainsCategoryAfterReload()
    {
        using var store = new Store(Root());
        await store.Collection.SaveAsync(Entry("A", "A"), true);
        await store.Collection.SaveAsync(Entry("B", "B"), true);
        using var log = new AppLogService(store.Paths);
        var library = new AnimeLibraryViewModel(Mock.Of<INavigationService>(), store.Collection, store.Covers, log, new(),
            Mock.Of<IAnimeUiService>(), (key, args) => key + string.Join(",", args));
        library.Search = "unrelated"; library.SelectedTag = "unrelated";
        library.ApplyRequest(new(AnimationStudio: "A")); await library.ActivateAsync();
        Assert.That(library.Items.Single().Title, Is.EqualTo("A"));
        Assert.That(library.Search, Is.Empty); Assert.That(library.SelectedTag, Is.Null);
        library.OnNavigatedFrom(); await library.ActivateAsync();
        Assert.That(library.AnimationStudio, Is.EqualTo("A"));
        library.ClearFiltersCommand.Execute(null);
        Assert.That(library.Items, Has.Count.EqualTo(2));
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task BackupV1_IsExplicitlyCompatibleAndRejectsDisguisedV2Fields(bool stripExtension)
    {
        using var source = new Store(Root()); using var target = new Store(Root());
        AnimeEntry saved = await source.Collection.SaveAsync(Entry("old format"), true);
        string personal = Personal((await source.Collection.GetByIdAsync(saved.Id))!);
        string file = Path.Combine(source.Paths.RootPath, "legacy.zip");
        await source.Backup.ExportAsync(file);
        Dictionary<string, byte[]> files;
        using (var input = ZipFile.OpenRead(file)) files = input.Entries.ToDictionary(e => e.FullName, e =>
        { using Stream stream = e.Open(); using var bytes = new MemoryStream(); stream.CopyTo(bytes); return bytes.ToArray(); });
        JsonObject manifest = JsonNode.Parse(files["manifest.json"])!.AsObject();
        JsonArray rows = JsonNode.Parse(files["anime.json"])!.AsArray();
        if (stripExtension) rows[0]!["metadata"]!.AsObject().Remove("animationStudios");
        files["anime.json"] = JsonSerializer.SerializeToUtf8Bytes(rows, BackupFormat.JsonOptions);
        manifest["exportFormatVersion"] = 1;
        manifest["animeSha256"] = Convert.ToHexString(SHA256.HashData(files["anime.json"]));
        files["manifest.json"] = JsonSerializer.SerializeToUtf8Bytes(manifest, BackupFormat.JsonOptions);
        using (var output = new ZipArchive(new FileStream(file, FileMode.Create), ZipArchiveMode.Create))
            foreach (var pair in files) { using Stream stream = output.CreateEntry(pair.Key).Open(); stream.Write(pair.Value); }
        if (!stripExtension)
        {
            Assert.ThrowsAsync<InvalidDataException>(async () => { using var invalid = await target.Backup.PrepareAsync(file); });
            Assert.That(await target.Collection.GetAllAsync(), Is.Empty);
            return;
        }
        using var prepared = await target.Backup.PrepareAsync(file);
        await target.Backup.RestoreAsync(prepared);
        AnimeEntry after = (await target.Collection.GetByIdAsync(saved.Id))!;
        Assert.That(after.Metadata.AnimationStudios, Is.Empty);
        Assert.That(Personal(after), Is.EqualTo(personal));
    }
}
