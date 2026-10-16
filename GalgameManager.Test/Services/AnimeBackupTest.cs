using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using LemonVN.Application.Anime;
using LemonVN.Contracts.Services;
using LemonVN.Domain.Anime;
using LemonVN.Infrastructure.Anime;
using LemonVN.Infrastructure.Backup;
using LemonVN.Services;
using Moq;

namespace GalgameManager.Test.Services;

[TestFixture]
public sealed class AnimeBackupTest
{
    private sealed class Store : IDisposable
    {
        public AppDataPathService Paths { get; }
        public AppDatabaseService Database { get; }
        public LiteDbAnimeRepository Repository { get; }
        public AnimeCollectionService Collection { get; }
        public CoverStorageService Covers { get; }
        public AnimeBackupService Backup { get; }
        public Store(string root)
        {
            Paths = new(root);
            Database = new(Paths);
            Database.Initialize();
            Repository = new(Database);
            Covers = new(Paths);
            var log = new AppLogService(Paths);
            Collection = new(Repository, Covers, log);
            Backup = new(Repository, Collection, Covers, Paths, log);
        }
        public void Dispose() => Database.Dispose();
    }

    private string _root = null!;
    private Store _source = null!;
    private Store _target = null!;
    private string _zip = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(TestEnvironmentSetup.Root, "M4Backup", Guid.NewGuid().ToString("N"));
        _source = new(Path.Combine(_root, "source"));
        _target = new(Path.Combine(_root, "target"));
        _zip = Path.Combine(_root, "backup.zip");
    }
    [TearDown] public void TearDown() { _source.Dispose(); _target.Dispose(); }

    private static string Picture()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            string file = Path.Combine(directory.FullName, "GalgameManager", "Assets", "LemonPlaceholder.png");
            if (File.Exists(file)) return file;
        }
        throw new FileNotFoundException("Missing owned test image.");
    }

    private static Task<AnimeEntry> Add(Store store, string title, bool cover = true) => store.Collection.SaveAsync(new()
    {
        Metadata = new() { Title = title, OriginalTitle = "Original " + title, Description = "Offline metadata", TotalEpisodes = 12, AirDate = new(2024, 4, 1) },
        Record = new() { WatchStatus = WatchStatus.OnHold, PersonalRating = 8, Tags = ["音乐", "Favourite"], Notes = "personal note",
            StartedAt = new(2024, 4, 2), CompletedAt = new(2024, 6, 2), Episodes = [new() { EpisodeNumber = 1, IsWatched = true,
                WatchedAtUtc = new(2024, 4, 3, 12, 0, 0, DateTimeKind.Utc) }, new() { EpisodeNumber = 4, IsWatched = true, Notes = "episode note" }] }
    }, true, cover ? Picture() : null);

    private async Task<string> ExportOne()
    {
        await Add(_source, "Source");
        await _source.Backup.ExportAsync(_zip);
        return _zip;
    }

    private static void Mutate(string path, Action<JsonObject, JsonArray> mutation, string? extraName = null, byte[]? extraBytes = null, bool updateHashes = true)
    {
        Dictionary<string, byte[]> files;
        using (var input = ZipFile.OpenRead(path))
            files = input.Entries.ToDictionary(entry => entry.FullName, entry =>
            {
                using Stream stream = entry.Open();
                using var bytes = new MemoryStream();
                stream.CopyTo(bytes);
                return bytes.ToArray();
            });
        JsonObject manifest = JsonNode.Parse(files["manifest.json"])!.AsObject();
        JsonArray entries = JsonNode.Parse(files["anime.json"])!.AsArray();
        mutation(manifest, entries);
        files["anime.json"] = JsonSerializer.SerializeToUtf8Bytes(entries, BackupFormat.JsonOptions);
        if (updateHashes) manifest["animeSha256"] = Convert.ToHexString(SHA256.HashData(files["anime.json"]));
        files["manifest.json"] = JsonSerializer.SerializeToUtf8Bytes(manifest, BackupFormat.JsonOptions);
        if (extraName is not null) files[extraName] = extraBytes ?? [1, 2, 3];
        using var outputFile = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var output = new ZipArchive(outputFile, ZipArchiveMode.Create);
        foreach (var (name, bytes) in files)
        {
            using Stream stream = output.CreateEntry(name).Open();
            stream.Write(bytes);
        }
    }

    // Rewrite only a known test ZIP, not a user-selected path.
    private void Rewrite(Action<JsonObject, JsonArray> mutation, string? extraName = null, byte[]? extraBytes = null, bool updateHashes = true)
    {
        string copy = Path.Combine(_root, "rewrite.zip");
        File.Move(_zip, copy);
        Mutate(copy, mutation, extraName, extraBytes, updateHashes);
        File.Move(copy, _zip);
    }

    [Test]
    public async Task CompletePortableBackup_RestoresMetadataPersonalRecordsCoversAndTrash_WithOpenDatabase()
    {
        AnimeEntry live = await Add(_source, "Live");
        AnimeEntry archived = await Add(_source, "Archived");
        await _source.Collection.DeleteAsync(archived);
        AnimeEntry old = await Add(_target, "Previous target");
        await _source.Backup.ExportAsync(_zip);
        using (var zip = ZipFile.OpenRead(_zip))
        {
            Assert.That(zip.Entries.Select(entry => entry.FullName), Does.Contain("manifest.json").And.Contain("anime.json").And.Contain("trash.json"));
            using var text = new StreamReader(zip.GetEntry("anime.json")!.Open());
            Assert.That(await text.ReadToEndAsync(), Does.Not.Contain(_source.Paths.RootPath).And.Not.Contain("watchedCount"));
        }
        using PreparedBackup plan = await _target.Backup.PrepareAsync(_zip);
        Assert.That(plan.AnimeCount, Is.EqualTo(1));
        Assert.That(plan.TrashCount, Is.EqualTo(1));
        BackupRestoreResult result = await _target.Backup.RestoreAsync(plan);
        Assert.That(_target.Database.IsOpen, Is.True);
        AnimeEntry restored = (await _target.Repository.GetByIdAsync(live.Id))!;
        Assert.That(restored.Metadata.Title, Is.EqualTo(live.Metadata.Title));
        Assert.That(restored.Metadata.AirDate, Is.EqualTo(live.Metadata.AirDate));
        Assert.That(restored.CreatedAtUtc, Is.EqualTo((await _source.Repository.GetByIdAsync(live.Id))!.CreatedAtUtc));
        Assert.That(restored.Record.WatchStatus, Is.EqualTo(WatchStatus.OnHold));
        Assert.That(restored.Record.WatchedCount, Is.EqualTo(2));
        Assert.That(restored.Record.PersonalRating, Is.EqualTo(8));
        Assert.That(restored.Record.Tags, Is.EqualTo(live.Record.Tags));
        Assert.That(restored.Record.StartedAt, Is.EqualTo(live.Record.StartedAt));
        Assert.That(restored.Record.CompletedAt, Is.EqualTo(live.Record.CompletedAt));
        Assert.That(restored.Record.Notes, Is.EqualTo(live.Record.Notes));
        Assert.That(restored.Record.Episodes[1].Notes, Is.EqualTo("episode note"));
        Assert.That(await _target.Covers.ReadOwnedAsync(restored.Metadata.CoverRef!, default), Is.EqualTo(File.ReadAllBytes(Picture())));
        Assert.That(await _target.Repository.GetTrashAsync(), Has.Count.EqualTo(1));
        Assert.That(File.Exists(result.RecoveryBackupPath), Is.True);
        Assert.That(File.Exists(Path.Combine(_target.Paths.RootPath, old.Metadata.CoverRef!)), Is.True);
        _target.Dispose();
        _target = new(_target.Paths.RootPath);
        Assert.That((await _target.Repository.GetByIdAsync(live.Id))!.Record.WatchedCount, Is.EqualTo(2));
    }

    [Test]
    public async Task EmptyBackup_ReplacesOnlyAfterRecoveryCopy_AndKeepsOldCovers()
    {
        AnimeEntry old = await Add(_target, "Old");
        await _source.Backup.ExportAsync(_zip);
        using PreparedBackup plan = await _target.Backup.PrepareAsync(_zip);
        BackupRestoreResult result = await _target.Backup.RestoreAsync(plan);
        Assert.That(await _target.Collection.GetAllAsync(), Is.Empty);
        Assert.That(File.Exists(result.RecoveryBackupPath), Is.True);
        Assert.That(File.Exists(Path.Combine(_target.Paths.RootPath, old.Metadata.CoverRef!)), Is.True);
    }

    [TestCase("version")]
    [TestCase("missingVersion")]
    [TestCase("unknownManifestField")]
    [TestCase("unknownRecordField")]
    [TestCase("missingGuid")]
    [TestCase("invalidGuid")]
    [TestCase("duplicateGuid")]
    [TestCase("schema")]
    [TestCase("duplicateEpisode")]
    [TestCase("invalidEpisode")]
    [TestCase("absoluteCover")]
    [TestCase("nullTag")]
    [TestCase("controlTag")]
    [TestCase("longTag")]
    [TestCase("excessTags")]
    public async Task InvalidData_IsRejectedBeforeReplacingCurrentCollection(string defect)
    {
        await ExportOne();
        await Add(_target, "Must survive");
        string before = (await _target.Repository.GetSnapshotAsync()).Signature;
        Rewrite((manifest, entries) =>
        {
            JsonObject entry = entries[0]!.AsObject();
            switch (defect)
            {
                case "version": manifest["exportFormatVersion"] = 99; break;
                case "missingVersion": manifest.Remove("exportFormatVersion"); break;
                case "unknownManifestField": manifest["futureMeaning"] = true; break;
                case "unknownRecordField": entry["record"]!["futureMeaning"] = true; break;
                case "missingGuid": entry.Remove("id"); break;
                case "invalidGuid": entry["id"] = "not-a-guid"; break;
                case "duplicateGuid": entries.Add(entry.DeepClone()); manifest["animeCount"] = 2; break;
                case "schema": entry["schemaVersion"] = 99; break;
                case "duplicateEpisode": entry["record"]!["episodes"]!.AsArray().Add(entry["record"]!["episodes"]![0]!.DeepClone()); break;
                case "invalidEpisode": entry["record"]!["episodes"]![0]!["episodeNumber"] = 0; break;
                case "absoluteCover": entry["metadata"]!["coverRef"] = "C:/outside.png"; break;
                case "nullTag": entry["record"]!["tags"]!.AsArray().Add((JsonNode?)null); break;
                case "controlTag": entry["record"]!["tags"]!.AsArray().Add("unsafe\u0001tag"); break;
                case "longTag": entry["record"]!["tags"]!.AsArray().Add(new string('x', AnimeTagRules.MaxLength + 1)); break;
                case "excessTags":
                    for (int index = 0; index <= AnimeTagRules.MaxTags; index++) entry["record"]!["tags"]!.AsArray().Add($"tag-{index}");
                    break;
            }
        });
        Assert.That(async () => await _target.Backup.PrepareAsync(_zip), Throws.Exception);
        Assert.That((await _target.Repository.GetSnapshotAsync()).Signature, Is.EqualTo(before));
        Assert.That(Directory.Exists(_target.Paths.BackupsPath), Is.False);
    }

    [TestCase("../outside.txt")]
    [TestCase("C:/outside.png")]
    [TestCase("\\\\server\\outside.png")]
    [TestCase("covers/../outside.png")]
    [TestCase("extra.json")]
    public async Task MaliciousZipPaths_AreRejectedWithoutExtraction(string path)
    {
        await ExportOne();
        Rewrite((_, _) => { }, path);
        Assert.ThrowsAsync<InvalidDataException>(() => _target.Backup.PrepareAsync(_zip));
        Assert.That(Directory.Exists(_target.Paths.TempPath), Is.False);
    }

    [Test]
    public async Task DuplicateZipEntries_AreRejected()
    {
        await ExportOne();
        using (var zip = ZipFile.Open(_zip, ZipArchiveMode.Update))
        {
            using Stream stream = zip.CreateEntry("anime.json").Open();
            stream.Write([1]);
        }
        Assert.ThrowsAsync<InvalidDataException>(() => _target.Backup.PrepareAsync(_zip));
    }

    [Test]
    public async Task CorruptZipAndChecksum_AreRejected()
    {
        await ExportOne();
        Rewrite((_, entries) => entries[0]!["metadata"]!["title"] = "tampered", updateHashes: false);
        Assert.ThrowsAsync<InvalidDataException>(() => _target.Backup.PrepareAsync(_zip));
        File.WriteAllText(_zip, "broken ZIP");
        Assert.ThrowsAsync<InvalidDataException>(() => _target.Backup.PrepareAsync(_zip));
    }

    [Test]
    public async Task OversizedExpandedEntry_IsRejectedBeforeJsonParsing()
    {
        await ExportOne();
        using (var zip = ZipFile.Open(_zip, ZipArchiveMode.Update))
        {
            zip.GetEntry("anime.json")!.Delete();
            using Stream stream = zip.CreateEntry("anime.json", CompressionLevel.Optimal).Open();
            byte[] buffer = new byte[1024 * 1024];
            for (int number = 0; number < 33; number++) stream.Write(buffer);
        }
        Assert.ThrowsAsync<InvalidDataException>(() => _target.Backup.PrepareAsync(_zip));
    }

    [Test]
    public async Task CancellationAndTargetChanges_DoNotReplaceData()
    {
        await ExportOne();
        AnimeEntry old = await Add(_target, "Old");
        using PreparedBackup plan = await _target.Backup.PrepareAsync(_zip);
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        Assert.ThrowsAsync<OperationCanceledException>(() => _target.Backup.RestoreAsync(plan, cancel.Token));
        old.Record.Notes = "newer target note";
        await _target.Collection.SaveAsync(old, false);
        Assert.ThrowsAsync<AnimeConflictException>(() => _target.Backup.RestoreAsync(plan));
        Assert.That((await _target.Repository.GetByIdAsync(old.Id))!.Record.Notes, Is.EqualTo("newer target note"));
    }

    [Test]
    public async Task CancellationAfterCopyingFirstCover_PreservesOriginalDatabaseAndRemovesOnlyStagedAsset()
    {
        await ExportOne();
        AnimeEntry old = await Add(_target, "Original");
        using PreparedBackup plan = await _target.Backup.PrepareAsync(_zip);
        using var cancel = new CancellationTokenSource();
        string? staged = null;
        var interruptedCovers = new Mock<ICoverStorageService>();
        interruptedCovers.Setup(service => service.ReadOwnedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string reference, CancellationToken token) => _target.Covers.ReadOwnedAsync(reference, token));
        interruptedCovers.Setup(service => service.ImportBytesAsync(It.IsAny<Guid>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns(async (Guid id, byte[] bytes, CancellationToken token) =>
            {
                staged = await _target.Covers.ImportBytesAsync(id, bytes, token);
                cancel.Cancel(); // Interrupt after real image IO, not before restore starts.
                return staged;
            });
        interruptedCovers.Setup(service => service.RemoveUncommitted(It.IsAny<string>()))
            .Callback((string reference) => _target.Covers.RemoveUncommitted(reference));
        var backup = new AnimeBackupService(_target.Repository, _target.Collection, interruptedCovers.Object,
            _target.Paths, new AppLogService(_target.Paths));
        Assert.That(async () => await backup.RestoreAsync(plan, cancel.Token), Throws.InstanceOf<OperationCanceledException>());
        Assert.That(staged, Is.Not.Null);
        AnimeEntry saved = (await _target.Collection.GetAllAsync()).Single();
        Assert.That(saved.Id, Is.EqualTo(old.Id));
        Assert.That(saved.Revision, Is.EqualTo(old.Revision));
        Assert.That(saved.Metadata.CoverRef, Is.EqualTo(old.Metadata.CoverRef));
        Assert.That(await _target.Covers.ReadOwnedAsync(saved.Metadata.CoverRef!, default), Is.EqualTo(File.ReadAllBytes(Picture())));
        Assert.That(File.Exists(Path.Combine(_target.Paths.RootPath, staged!)), Is.False);
        Assert.That(_target.Covers.ListOwnedReferences(), Has.Count.EqualTo(1));
        Assert.That(Directory.GetFiles(_target.Paths.BackupsPath, "*.zip"), Has.Length.EqualTo(1));
    }

    [Test]
    public async Task MidTransactionFailure_RollsBackDeletionAndKeepsOriginalCoverReferences()
    {
        await Add(_source, "Duplicate title");
        await Add(_source, "Duplicate title");
        AnimeEntry old = await Add(_target, "Original");
        _target.Database.Write(db => db.GetCollection<AnimeEntry>("anime").EnsureIndex("forced_failure", "$.Metadata.Title", unique: true));
        await _source.Backup.ExportAsync(_zip);
        using PreparedBackup plan = await _target.Backup.PrepareAsync(_zip);
        Assert.That(async () => await _target.Backup.RestoreAsync(plan), Throws.Exception);
        AnimeEntry saved = (await _target.Collection.GetAllAsync()).Single();
        Assert.That(saved.Id, Is.EqualTo(old.Id));
        Assert.That(saved.Metadata.CoverRef, Is.EqualTo(old.Metadata.CoverRef));
        Assert.That(File.ReadAllBytes(Path.Combine(_target.Paths.RootPath, saved.Metadata.CoverRef!)), Is.EqualTo(File.ReadAllBytes(Picture())));
        Assert.That(Directory.GetFiles(_target.Paths.BackupsPath, "*.zip"), Has.Length.EqualTo(1));
        Assert.That(_target.Covers.ListOwnedReferences(), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task RestoreBumpsRevisions_AndRejectsEditorsFromBeforeReplacement()
    {
        AnimeEntry old = await Add(_target, "Old", cover: false);
        AnimeEntry source = old.Copy();
        source.Metadata.Title = "Imported";
        await _source.Repository.AddAsync(source);
        await _source.Backup.ExportAsync(_zip);
        using PreparedBackup plan = await _target.Backup.PrepareAsync(_zip);
        await _target.Backup.RestoreAsync(plan);
        Assert.ThrowsAsync<AnimeConflictException>(() => _target.Collection.SaveAsync(old, false));
        Assert.That((await _target.Repository.GetByIdAsync(old.Id))!.Metadata.Title, Is.EqualTo("Imported"));
    }

    [Test]
    public async Task RecycleRestoreAndPurge_AreRevisionSafe_AndPreviewNeverDeletesReferencedCovers()
    {
        AnimeEntry entry = await Add(_target, "Recycle");
        await _target.Collection.DeleteAsync(entry);
        Assert.That(await _target.Backup.GetUnreferencedCoverPreviewAsync(), Is.Empty);
        AnimeEntry restored = await _target.Repository.RestoreAsync(entry.Id, entry.Revision);
        await _target.Collection.DeleteAsync(restored);
        string recovery = await _target.Backup.PermanentlyDeleteAsync(restored);
        Assert.That(await _target.Repository.GetTrashAsync(), Is.Empty);
        Assert.That(File.Exists(recovery), Is.True);
        Assert.That(await _target.Backup.GetUnreferencedCoverPreviewAsync(), Has.Count.EqualTo(1));
        Assert.That(File.Exists(Path.Combine(_target.Paths.RootPath, entry.Metadata.CoverRef!)), Is.True);
        AnimeEntry readded = await _target.Collection.SaveAsync(new() { Id = entry.Id, Metadata = new() { Title = "Readded" } }, true);
        Assert.That(readded.Revision, Is.GreaterThan(restored.Revision));
        Assert.ThrowsAsync<AnimeConflictException>(() => _target.Collection.UpdateRecordAsync(readded.Id, restored.Revision, restored.Record));
    }

    [Test]
    public async Task ExportCancellationOrFailure_DoesNotReplacePreviousBackupWithPartialZip()
    {
        await ExportOne();
        byte[] original = File.ReadAllBytes(_zip);
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        Assert.ThrowsAsync<OperationCanceledException>(() => _source.Backup.ExportAsync(_zip, cancel.Token));
        Assert.That(File.ReadAllBytes(_zip), Is.EqualTo(original));
        using (var locked = new FileStream(_zip, FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.ThrowsAsync<IOException>(() => _source.Backup.ExportAsync(_zip));
        Assert.That(File.ReadAllBytes(_zip), Is.EqualTo(original));
        Assert.That(Directory.GetFiles(_root, "*.tmp"), Is.Empty);
    }
}
