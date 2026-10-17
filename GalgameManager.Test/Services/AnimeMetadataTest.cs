using System.Text.Json;
using LemonVN.Application.Anime;
using LemonVN.Contracts.Services;
using LemonVN.Domain.Anime;
using LemonVN.Infrastructure.Anime;
using LemonVN.Infrastructure.Backup;
using LemonVN.Infrastructure.Bangumi;
using LemonVN.Services;
using Moq;

namespace GalgameManager.Test.Services;

[TestFixture]
public sealed class AnimeMetadataTest
{
    private AppDataPathService _paths = null!;
    private AppDatabaseService _database = null!;
    private LiteDbAnimeRepository _repository = null!;
    private CoverStorageService _covers = null!;
    private AnimeCollectionService _collection = null!;
    private Mock<IBangumiAnimeProvider> _provider = null!;
    private Mock<IRemoteAnimeCoverCache> _remote = null!;
    private AnimeMetadataService _metadata = null!;
    private readonly BangumiAnime _source = new("328609", "网络名称", "Network original", "Network summary",
        new(2022, 10, 8), 12, new("https://lain.bgm.tv/pic/cover/l/a.jpg"), null);

    [SetUp]
    public void SetUp()
    {
        _paths = new(Path.Combine(TestEnvironmentSetup.Root, "M5Metadata", Guid.NewGuid().ToString("N")));
        _database = new(_paths); _database.Initialize(); _repository = new(_database);
        _covers = new(_paths); _collection = new(_repository, _covers, new AppLogService(_paths));
        _provider = new(); _remote = new();
        _provider.Setup(provider => provider.GetAnimeDetailsAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(_source);
        _remote.Setup(covers => covers.GetBytesAsync(It.IsAny<Uri?>(), It.IsAny<CancellationToken>(), It.IsAny<bool>())).ReturnsAsync(Picture());
        _metadata = new(_provider.Object, _remote.Object, _collection, _covers);
    }
    [TearDown] public void TearDown() => _database.Dispose();

    private async Task<AnimeEntry> Import() => await _metadata.CommitAsync(await _metadata.PrepareAsync(_source.Id, null, false, default), default);

    [Test]
    public async Task ImportPreview_HasNoDatabaseWrite_AndConfirmedImportSurvivesReopen_WithIndependentIdentity()
    {
        AnimeMetadataPreview preview = await _metadata.PrepareAsync(_source.Id, null, false, default);
        Assert.That(await _collection.GetAllAsync(), Is.Empty);
        Assert.That(_covers.ListOwnedReferences(), Is.Empty);
        AnimeEntry entry = await _metadata.CommitAsync(preview, default);
        Assert.That(entry.Metadata.BangumiId, Is.EqualTo(_source.Id));
        Assert.That(entry.Id, Is.Not.EqualTo(Guid.Empty));
        Assert.That(entry.Metadata.UserOverrides, Is.Empty);
        Assert.That(entry.Record.PersonalRating, Is.Null);
        Assert.That(entry.Record.Episodes, Is.Empty);
        Assert.That(entry.Record.Tags, Is.Empty);
        Assert.That(File.Exists(Path.Combine(_paths.RootPath, entry.Metadata.CoverRef!)), Is.True);
        _database.Dispose(); _database = new(_paths); _database.Initialize();
        AnimeEntry loaded = (await new LiteDbAnimeRepository(_database).GetByIdAsync(entry.Id))!;
        Assert.That(loaded.Metadata.BangumiId, Is.EqualTo(_source.Id));
        Assert.That(loaded.Metadata.TotalEpisodes, Is.EqualTo(12));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task MissingOrFailedCover_StillAllowsMetadataImport(bool fails)
    {
        if (fails) _remote.Setup(covers => covers.GetBytesAsync(It.IsAny<Uri?>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ThrowsAsync(new BangumiRequestException(BangumiFailure.Timeout));
        else _remote.Setup(covers => covers.GetBytesAsync(It.IsAny<Uri?>(), It.IsAny<CancellationToken>(), It.IsAny<bool>())).ReturnsAsync((byte[]?)null);
        AnimeEntry entry = await Import();
        Assert.That(entry.Metadata.Title, Is.EqualTo(_source.Title));
        Assert.That(entry.Metadata.CoverRef, Is.Null);
    }

    [Test]
    public async Task DuplicateId_IsReportedWithExistingGuid_WhileSameTitleWithoutIdIsNotMerged()
    {
        AnimeEntry manual = await _collection.SaveAsync(new() { Metadata = new() { Title = _source.Title } }, true);
        AnimeEntry imported = await Import();
        Assert.That(imported.Id, Is.Not.EqualTo(manual.Id));
        var error = Assert.ThrowsAsync<AnimeAlreadyCollectedException>(() => _metadata.PrepareAsync(_source.Id, null, false, default));
        Assert.That(error!.ExistingId, Is.EqualTo(imported.Id));
        Assert.ThrowsAsync<AnimeAlreadyCollectedException>(() => _metadata.PrepareAsync(_source.Id, manual.Id, false, default));
        Assert.That(await _collection.GetAllAsync(), Has.Count.EqualTo(2));
    }

    [Test]
    public async Task LinkManualEntry_PreservesItsGuid_AllProtectedFields_AndPersonalRecord()
    {
        string picture = Path.Combine(_paths.RootPath, "manual-cover.png");
        Directory.CreateDirectory(_paths.RootPath); File.WriteAllBytes(picture, Picture());
        AnimeEntry manual = await _collection.SaveAsync(new()
        {
            Metadata = new() { Title = "Manual title", OriginalTitle = "My original title", Description = "My summary", TotalEpisodes = 20, AirDate = new(2020, 1, 1) },
            Record = PersonalRecord()
        }, true, picture);
        string personal = JsonSerializer.Serialize(manual.Record);
        AnimeMetadataPreview plan = await _metadata.PrepareAsync(_source.Id, manual.Id, false, default);
        Assert.That(plan.Changes.All(change => change.Kind == MetadataChangeKind.Protected), Is.True);
        AnimeEntry linked = await _metadata.CommitAsync(plan, default);
        Assert.That(linked.Id, Is.EqualTo(manual.Id));
        Assert.That(linked.Metadata.BangumiId, Is.EqualTo(_source.Id));
        Assert.That(linked.Metadata.Title, Is.EqualTo("Manual title"));
        Assert.That(linked.Metadata.Description, Is.EqualTo("My summary"));
        Assert.That(linked.Metadata.TotalEpisodes, Is.EqualTo(20));
        Assert.That(linked.Metadata.OriginalTitle, Is.EqualTo(manual.Metadata.OriginalTitle));
        Assert.That(linked.Metadata.AirDate, Is.EqualTo(manual.Metadata.AirDate));
        Assert.That(linked.Metadata.CoverRef, Is.EqualTo(manual.Metadata.CoverRef));
        Assert.That(JsonSerializer.Serialize(linked.Record), Is.EqualTo(personal));
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task TitleOnlyManualEntry_LinksIntoSamePersistedCollection_AndFillsMissingMetadata(bool legacyMask, bool alreadyLinked)
    {
        var source = _source with { AnimationStudios = ["Synthetic animation studio", "Second animation studio"] };
        _provider.Setup(provider => provider.GetAnimeDetailsAsync(source.Id, It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(source);
        AnimeEntry manual = await _collection.SaveAsync(new() { Metadata = new() { Title = "111" }, Record = PersonalRecord() }, true);
        Assert.That(manual.Metadata.UserOverrides, Is.EqualTo(new[] { "Title" }));
        if (legacyMask)
        {
            // Reproduce v0.1.0 data without opening any user's database.
            manual.Metadata.UserOverrides.UnionWith(new[] { "OriginalTitle", "Description", "TotalEpisodes", "AirDate", "CoverRef" });
            if (alreadyLinked) manual.Metadata.BangumiId = source.Id;
            manual = await _repository.UpdateAsync(manual);
        }
        manual = (await _repository.GetByIdAsync(manual.Id))!;
        string original = JsonSerializer.Serialize(manual), personal = JsonSerializer.Serialize(manual.Record);
        AnimeMetadataPreview preview = await _metadata.PrepareAsync(source.Id, manual.Id, alreadyLinked, default, fillLegacyEmptyFields: true);
        Assert.Multiple(() =>
        {
            Assert.That(preview.Changes.Single(change => change.Field == "Title").Kind, Is.EqualTo(MetadataChangeKind.Protected));
            Assert.That(preview.Changes.Where(change => change.Field != "Title").All(change => change.Kind == MetadataChangeKind.Update), Is.True);
            Assert.That(preview.LegacyEmptyFieldsToFill.Count, Is.EqualTo(legacyMask ? 5 : 0));
        });
        Assert.That(JsonSerializer.Serialize((await _repository.GetByIdAsync(manual.Id))!), Is.EqualTo(original), "Preview must not unlock or write anything.");
        AnimeEntry saved = await _metadata.CommitAsync(preview, default);
        Assert.That(saved.Id, Is.EqualTo(manual.Id));
        Assert.That(saved.Revision, Is.EqualTo(manual.Revision + 1));
        Assert.That(await _repository.GetAllAsync(), Has.Count.EqualTo(1));
        Assert.That(saved.Metadata.UserOverrides, Is.EqualTo(new[] { "Title" }));
        Assert.That(await _covers.ReadOwnedAsync(saved.Metadata.CoverRef!, default), Is.EqualTo(Picture()));
        _database.Dispose(); _database = new(_paths); _database.Initialize();
        AnimeEntry reopened = (await new LiteDbAnimeRepository(_database).GetByIdAsync(manual.Id))!;
        Assert.Multiple(() =>
        {
            Assert.That(reopened.SchemaVersion, Is.EqualTo(1));
            Assert.That(reopened.Metadata.Title, Is.EqualTo("111"));
            Assert.That(reopened.Metadata.BangumiId, Is.EqualTo(source.Id));
            Assert.That(reopened.Metadata.OriginalTitle, Is.EqualTo(source.OriginalTitle));
            Assert.That(reopened.Metadata.Description, Is.EqualTo(source.Description));
            Assert.That(reopened.Metadata.TotalEpisodes, Is.EqualTo(source.TotalEpisodes));
            Assert.That(reopened.Metadata.AirDate, Is.EqualTo(source.AirDate));
            Assert.That(reopened.Metadata.AnimationStudios, Is.EqualTo(source.AnimationStudios));
            Assert.That(reopened.Metadata.CoverRef, Is.EqualTo(saved.Metadata.CoverRef));
            Assert.That(JsonSerializer.Serialize(reopened.Record), Is.EqualTo(personal));
        });
    }

    [Test]
    public async Task IntentionalEmptyOverride_IsProtected_AndLegacyRepairRequiresExplicitPreviewOption()
    {
        AnimeEntry manual = await _collection.SaveAsync(new() { Metadata = new() { Title = "111", Description = "My summary" } }, true);
        manual.Metadata.Description = null;
        manual = await _collection.SaveAsync(manual, false);
        AnimeMetadataPreview preview = await _metadata.PrepareAsync(_source.Id, manual.Id, false, default, fillLegacyEmptyFields: true);
        Assert.That(preview.Changes.Single(change => change.Field == "Description").Kind, Is.EqualTo(MetadataChangeKind.Protected));
        Assert.That(preview.LegacyEmptyFieldsToFill, Is.Empty);
        manual.Metadata.UserOverrides.UnionWith(new[] { "OriginalTitle", "TotalEpisodes", "AirDate", "CoverRef" });
        manual = await _repository.UpdateAsync(manual);
        preview = await _metadata.PrepareAsync(_source.Id, manual.Id, false, default);
        Assert.That(preview.Changes.All(change => change.Kind == MetadataChangeKind.Protected), Is.True);
        Assert.That(preview.LegacyEmptyFieldsToFill, Is.Empty);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task LegacyRepair_WithTemporarilyMissingMetadataOrCover_CanBeRefreshedLater(bool failedCover)
    {
        AnimeEntry manual = await _collection.SaveAsync(new() { Metadata = new() { Title = "111" } }, true);
        manual.Metadata.UserOverrides.UnionWith(new[] { "OriginalTitle", "Description", "AirDate", "TotalEpisodes", "CoverRef" });
        manual = await _repository.UpdateAsync(manual);
        if (failedCover)
            _remote.Setup(covers => covers.GetBytesAsync(It.IsAny<Uri?>(), It.IsAny<CancellationToken>(), It.IsAny<bool>())).ThrowsAsync(new IOException("Synthetic temporary failure"));
        else
        {
            _provider.Setup(provider => provider.GetAnimeDetailsAsync(_source.Id, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(_source with { OriginalTitle = null, Description = null, AirDate = null, TotalEpisodes = null, CoverUri = null });
            _remote.Setup(covers => covers.GetBytesAsync(It.IsAny<Uri?>(), It.IsAny<CancellationToken>(), It.IsAny<bool>())).ReturnsAsync((byte[]?)null);
        }
        AnimeMetadataPreview preview = await _metadata.PrepareAsync(_source.Id, manual.Id, false, default, fillLegacyEmptyFields: true);
        Assert.That(preview.LegacyEmptyFieldsToFill, Has.Count.EqualTo(5));
        AnimeEntry linked = await _metadata.CommitAsync(preview, default);
        Assert.That(linked.Metadata.UserOverrides, Is.EqualTo(new[] { "Title" }));
        Assert.That(linked.Metadata.CoverRef, Is.Null);
        if (!failedCover)
        {
            Assert.That(linked.Metadata.Description, Is.Null);
            Assert.That(linked.Metadata.TotalEpisodes, Is.Null);
        }
        _provider.Setup(provider => provider.GetAnimeDetailsAsync(_source.Id, It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(_source);
        _remote.Setup(covers => covers.GetBytesAsync(It.IsAny<Uri?>(), It.IsAny<CancellationToken>(), It.IsAny<bool>())).ReturnsAsync(Picture());
        linked = await _metadata.CommitAsync(await _metadata.PrepareAsync(_source.Id, linked.Id, true, default), default);
        Assert.Multiple(() =>
        {
            Assert.That(linked.Id, Is.EqualTo(manual.Id));
            Assert.That(linked.Metadata.Title, Is.EqualTo("111"));
            Assert.That(linked.Metadata.Description, Is.EqualTo(_source.Description));
            Assert.That(linked.Metadata.TotalEpisodes, Is.EqualTo(_source.TotalEpisodes));
            Assert.That(linked.Metadata.CoverRef, Is.Not.Null);
        });
        Assert.That(await _collection.GetAllAsync(), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task EditingPersonalFields_DoesNotLockNetworkMetadata_ButChangedNameIsProtectedDuringRefresh()
    {
        AnimeEntry entry = await Import();
        entry.Record = PersonalRecord();
        entry = await _collection.SaveAsync(entry, false);
        Assert.That(entry.Metadata.UserOverrides, Is.Empty);
        entry.Metadata.Title = "My protected name";
        entry = await _collection.SaveAsync(entry, false);
        Assert.That(entry.Metadata.UserOverrides, Is.EqualTo(new[] { "Title" }));
        string personal = JsonSerializer.Serialize(entry.Record);
        _provider.Setup(provider => provider.GetAnimeDetailsAsync(_source.Id, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_source with { Title = "Changed server name", Description = "Updated summary", TotalEpisodes = 2 });
        AnimeMetadataPreview plan = await _metadata.PrepareAsync(_source.Id, entry.Id, true, default);
        Assert.That(plan.Changes.Single(change => change.Field == "Title").Kind, Is.EqualTo(MetadataChangeKind.Protected));
        Assert.That(plan.HasOutOfRangeEpisodes, Is.True);
        AnimeEntry refreshed = await _metadata.CommitAsync(plan, default);
        Assert.That(refreshed.Metadata.Title, Is.EqualTo("My protected name"));
        Assert.That(refreshed.Metadata.Description, Is.EqualTo("Updated summary"));
        Assert.That(refreshed.Metadata.TotalEpisodes, Is.EqualTo(2));
        Assert.That(JsonSerializer.Serialize(refreshed.Record), Is.EqualTo(personal));
        Assert.That(refreshed.Record.Episodes.Single().EpisodeNumber, Is.EqualTo(11));
        Assert.That(refreshed.Record.WatchStatus, Is.EqualTo(WatchStatus.OnHold));
    }

    [Test]
    public async Task EmptyRemoteFields_AndCoverFailure_NeverClearExistingValuesOrOwnedCover()
    {
        AnimeEntry entry = await Import();
        string cover = entry.Metadata.CoverRef!;
        _provider.Setup(provider => provider.GetAnimeDetailsAsync(_source.Id, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_source with { Description = null, AirDate = null, TotalEpisodes = null });
        _remote.Setup(covers => covers.GetBytesAsync(It.IsAny<Uri?>(), It.IsAny<CancellationToken>(), true)).ThrowsAsync(new IOException("simulated network failure"));
        AnimeMetadataPreview plan = await _metadata.PrepareAsync(_source.Id, entry.Id, true, default);
        Assert.That(plan.CoverUnavailable, Is.True);
        AnimeEntry saved = await _metadata.CommitAsync(plan, default);
        Assert.That(saved.Metadata.Description, Is.EqualTo(entry.Metadata.Description));
        Assert.That(saved.Metadata.AirDate, Is.EqualTo(entry.Metadata.AirDate));
        Assert.That(saved.Metadata.TotalEpisodes, Is.EqualTo(entry.Metadata.TotalEpisodes));
        Assert.That(saved.Metadata.CoverRef, Is.EqualTo(cover));
        Assert.That(saved.Revision, Is.EqualTo(entry.Revision));
        Assert.That(File.Exists(Path.Combine(_paths.RootPath, cover)), Is.True);
    }

    [Test]
    public async Task RevisionChanges_DuringRequestAndAfterPreview_DoNotOverwriteCurrentPersonalData()
    {
        AnimeEntry entry = await Import();
        _provider.Setup(provider => provider.GetAnimeDetailsAsync(_source.Id, true, It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                AnimeEntry latest = (await _collection.GetByIdAsync(entry.Id))!;
                latest.Record.Notes = "edited while request ran";
                await _collection.SaveAsync(latest, false);
                return _source;
            });
        Assert.ThrowsAsync<AnimeConflictException>(() => _metadata.PrepareAsync(_source.Id, entry.Id, true, default));
        AnimeMetadataPreview plan = await _metadata.PrepareAsync(_source.Id, entry.Id, false, default);
        AnimeEntry newer = (await _collection.GetByIdAsync(entry.Id))!;
        newer.Record.Notes = "edited after preview";
        await _collection.SaveAsync(newer, false);
        Assert.ThrowsAsync<AnimeConflictException>(() => _metadata.CommitAsync(plan, default));
        Assert.That((await _collection.GetByIdAsync(entry.Id))!.Record.Notes, Is.EqualTo("edited after preview"));
    }

    [Test]
    public async Task FailedOrCancelledRefresh_AndDeletedEntry_NeverRecreateOrMutateData()
    {
        AnimeEntry entry = await Import();
        _provider.Setup(provider => provider.GetAnimeDetailsAsync(_source.Id, true, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BangumiRequestException(BangumiFailure.Network));
        Assert.ThrowsAsync<BangumiRequestException>(() => _metadata.PrepareAsync(_source.Id, entry.Id, true, default));
        Assert.That((await _collection.GetByIdAsync(entry.Id))!.Revision, Is.EqualTo(entry.Revision));
        AnimeMetadataPreview plan = await _metadata.PrepareAsync(_source.Id, entry.Id, false, default);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.That(async () => await _metadata.CommitAsync(plan, cancelled.Token), Throws.InstanceOf<OperationCanceledException>());
        await _collection.DeleteAsync(entry);
        Assert.ThrowsAsync<KeyNotFoundException>(() => _metadata.CommitAsync(plan, default));
        Assert.That(await _collection.GetAllAsync(), Is.Empty);
        Assert.That(File.Exists(Path.Combine(_paths.RootPath, entry.Metadata.CoverRef!)), Is.True);
    }

    [Test]
    public async Task BangumiMetadata_AndOverrides_RoundTripThroughUnchangedM4BackupFormat_WithCrossRootCovers()
    {
        AnimeEntry entry = await Import();
        entry.Metadata.Title = "Protected across backup"; entry.Record = PersonalRecord();
        entry = await _collection.SaveAsync(entry, false);
        var log = new AppLogService(_paths);
        var backup = new AnimeBackupService(_repository, _collection, _covers, _paths, log);
        string file = Path.Combine(_paths.RootPath, "m4-format.zip");
        await backup.ExportAsync(file);
        var targetPaths = new AppDataPathService(Path.Combine(TestEnvironmentSetup.Root, "M5Restore", Guid.NewGuid().ToString("N")));
        using var targetDatabase = new AppDatabaseService(targetPaths); targetDatabase.Initialize();
        var targetRepository = new LiteDbAnimeRepository(targetDatabase);
        var targetCovers = new CoverStorageService(targetPaths);
        var targetCollection = new AnimeCollectionService(targetRepository, targetCovers, new AppLogService(targetPaths));
        var restore = new AnimeBackupService(targetRepository, targetCollection, targetCovers, targetPaths, new AppLogService(targetPaths));
        using PreparedBackup plan = await restore.PrepareAsync(file);
        await restore.RestoreAsync(plan);
        AnimeEntry restored = (await targetRepository.GetByIdAsync(entry.Id))!;
        Assert.That(restored.SchemaVersion, Is.EqualTo(1));
        Assert.That(restored.Metadata.BangumiId, Is.EqualTo(_source.Id));
        Assert.That(restored.Metadata.UserOverrides, Is.EqualTo(entry.Metadata.UserOverrides));
        Assert.That(JsonSerializer.Serialize(restored.Record), Is.EqualTo(JsonSerializer.Serialize(entry.Record)));
        Assert.That(await targetCovers.ReadOwnedAsync(restored.Metadata.CoverRef!, default), Is.EqualTo(Picture()));
    }

    private static AnimeRecord PersonalRecord() => new()
    {
        WatchStatus = WatchStatus.OnHold, PersonalRating = 9, Notes = "Private note must stay local", Tags = ["personal tag"],
        StartedAt = new(2024, 1, 1), CompletedAt = new(2024, 6, 1),
        Episodes = [new() { EpisodeNumber = 11, IsWatched = true, Notes = "My episode note", WatchedAtUtc = new(2024, 1, 2, 12, 0, 0, DateTimeKind.Utc) }]
    };

    [Test]
    public async Task MetadataWriteFailure_RemovesOnlyNewCover_AndPreservesExistingDataAndImage()
    {
        AnimeEntry old = await Import();
        old = (await _repository.GetByIdAsync(old.Id))!; // Compare persisted snapshots; LiteDB stores millisecond timestamps.
        string original = JsonSerializer.Serialize(old);
        var failing = new Mock<IAnimeRepository>();
        failing.Setup(repo => repo.GetByIdAsync(old.Id)).ReturnsAsync(() => old.Copy());
        failing.Setup(repo => repo.GetAllAsync()).ReturnsAsync(new[] { old.Copy() });
        failing.Setup(repo => repo.UpdateAsync(It.IsAny<AnimeEntry>())).ThrowsAsync(new IOException("simulated write failure"));
        var collection = new AnimeCollectionService(failing.Object, _covers, new AppLogService(_paths));
        AnimeMetadata proposed = old.Copy().Metadata;
        proposed.Description = "Must not persist";
        Assert.ThrowsAsync<IOException>(() => collection.SaveProviderMetadataAsync(old.Id, old.Revision, proposed, Picture(), default));
        Assert.That(JsonSerializer.Serialize((await _repository.GetByIdAsync(old.Id))!), Is.EqualTo(original));
        Assert.That(_covers.ListOwnedReferences(), Has.Count.EqualTo(1));
        Assert.That(await _covers.ReadOwnedAsync(old.Metadata.CoverRef!, default), Is.EqualTo(Picture()));
    }

    private static byte[] Picture()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            string file = Path.Combine(directory.FullName, "GalgameManager", "Assets", "LemonPlaceholder.png");
            if (File.Exists(file)) return File.ReadAllBytes(file);
        }
        throw new FileNotFoundException("Missing owned fixture image.");
    }
}
