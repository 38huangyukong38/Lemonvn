using JsonSerializer = System.Text.Json.JsonSerializer;
using LemonVN.Contracts.Services;
using LemonVN.Domain.Anime;
using LemonVN.Infrastructure.Anime;
using LemonVN.Services;
using LiteDB;

namespace GalgameManager.Test.Services;

[TestFixture]
public sealed class AnimeRepositoryTest
{
    private AppDataPathService _paths = null!;
    private AppDatabaseService _database = null!;
    private IAnimeRepository _repository = null!;

    [SetUp]
    public void SetUp()
    {
        _paths = new(Path.Combine(TestEnvironmentSetup.Root, "Anime", Guid.NewGuid().ToString("N")));
        _database = new(_paths);
        _database.Initialize();
        _repository = new LiteDbAnimeRepository(_database);
    }

    [TearDown] public void TearDown() => _database.Dispose();

    [Test]
    public async Task TenManualEntries_WithOptionalFieldsEmpty_SurviveDatabaseRestart()
    {
        var ids = new List<Guid>();
        for (var i = 0; i < 10; i++)
        {
            AnimeEntry entry = await _repository.AddAsync(new() { Metadata = new() { Title = $"Anime {i}" } });
            ids.Add(entry.Id);
        }
        _database.Dispose();
        _database = new(_paths);
        _database.Initialize();
        _repository = new LiteDbAnimeRepository(_database);
        Assert.That(await _repository.GetAllAsync(), Has.Count.EqualTo(10));
        foreach (Guid id in ids)
        {
            AnimeEntry? loaded = await _repository.GetByIdAsync(id);
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded!.Metadata.TotalEpisodes, Is.Null);
            Assert.That(loaded.Record.PersonalRating, Is.Null);
        }
    }

    [Test]
    public async Task UpdateMetadataAndRecord_PreservesIdDatesEpisodesAndIndependentStatus()
    {
        AnimeEntry entry = await _repository.AddAsync(new()
        {
            Metadata = new() { Title = "Original", TotalEpisodes = 12, AirDate = new DateOnly(2024, 4, 1) },
            Record = new()
            {
                WatchStatus = WatchStatus.OnHold, PersonalRating = 0,
                Episodes = [new() { EpisodeNumber = 1, IsWatched = true }, new() { EpisodeNumber = 4, IsWatched = true, Notes = "keep" }]
            }
        });
        DateTime created = entry.CreatedAtUtc;
        entry.Metadata.Title = "Edited";
        entry.Metadata.TotalEpisodes = 2;
        entry.Record.Notes = "personal note";
        entry.Record.PersonalRating = 10;
        await _repository.UpdateAsync(entry);
        AnimeEntry loaded = (await _repository.GetByIdAsync(entry.Id))!;
        Assert.That(loaded.Metadata.Title, Is.EqualTo("Edited"));
        Assert.That(loaded.Metadata.AirDate, Is.EqualTo(new DateOnly(2024, 4, 1)));
        Assert.That(loaded.CreatedAtUtc, Is.EqualTo(created).Within(TimeSpan.FromMilliseconds(1)));
        Assert.That(loaded.Record.WatchStatus, Is.EqualTo(WatchStatus.OnHold));
        Assert.That(loaded.Record.WatchedCount, Is.EqualTo(2));
        Assert.That(loaded.Record.Episodes.Single(x => x.EpisodeNumber == 4).Notes, Is.EqualTo("keep"));
        Assert.That(loaded.Record.Notes, Is.EqualTo("personal note"));
        Assert.That(loaded.Record.PersonalRating, Is.EqualTo(10));
    }

    [Test]
    public async Task Deletion_IsRecoverable_AfterRestart()
    {
        AnimeEntry entry = await _repository.AddAsync(new() { Metadata = new() { Title = "Keep a snapshot" } });
        await _repository.DeleteAsync(entry.Id, entry.Revision);
        Assert.That(await _repository.GetByIdAsync(entry.Id), Is.Null);
        _database.Dispose();
        _database = new(_paths);
        _database.Initialize();
        _repository = new LiteDbAnimeRepository(_database);
        AnimeEntry restored = await _repository.RestoreAsync(entry.Id);
        Assert.That(restored.Metadata.Title, Is.EqualTo("Keep a snapshot"));
        Assert.That(await _repository.GetAllAsync(), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task StaleUpdateAndDelete_CannotOverwriteOrResurrectData()
    {
        AnimeEntry entry = await _repository.AddAsync(new() { Metadata = new() { Title = "First" } });
        AnimeEntry stale = entry.Copy();
        entry.Metadata.Title = "Newer";
        AnimeEntry updated = await _repository.UpdateAsync(entry);
        Assert.ThrowsAsync<AnimeConflictException>(() => _repository.UpdateAsync(stale));
        Assert.ThrowsAsync<AnimeConflictException>(() => _repository.DeleteAsync(stale.Id, stale.Revision));
        await _repository.DeleteAsync(updated.Id, updated.Revision);
        Assert.ThrowsAsync<KeyNotFoundException>(() => _repository.UpdateAsync(updated));
    }

    [TestCase(0)] [TestCase(-1)]
    public void InvalidEpisodes_AreRejectedWithoutWrites(int total)
    {
        Assert.ThrowsAsync<AnimeValidationException>(() => _repository.AddAsync(new()
            { Metadata = new() { Title = "Invalid", TotalEpisodes = total } }));
    }

    [TestCase(-1)] [TestCase(11)]
    public void InvalidRating_IsRejected(int rating) =>
        Assert.ThrowsAsync<AnimeValidationException>(() => _repository.AddAsync(new()
            { Metadata = new() { Title = "Invalid" }, Record = new() { PersonalRating = rating } }));

    [TestCase("")] [TestCase("   ")]
    public void EmptyTitle_IsRejected(string title) => Assert.ThrowsAsync<AnimeValidationException>(() =>
        _repository.AddAsync(new() { Metadata = new() { Title = title } }));

    [Test]
    public async Task EpisodeRoundTrip_HasOneAuthority_AndRejectsDuplicateNumbers()
    {
        var entry = new AnimeEntry
        {
            Metadata = new() { Title = "Episodes" },
            Record = new() { Episodes = [new() { EpisodeNumber = 7, IsWatched = true, WatchedAtUtc = DateTime.UtcNow, Notes = "episode note" }] }
        };
        string json = JsonSerializer.Serialize(entry);
        Assert.That(json, Does.Not.Contain("WatchedCount"));
        AnimeEntry deserialized = JsonSerializer.Deserialize<AnimeEntry>(json)!;
        AnimeEntry saved = await _repository.AddAsync(deserialized);
        AnimeEntry loaded = (await _repository.GetByIdAsync(saved.Id))!;
        Assert.That(loaded.Record.WatchedCount, Is.EqualTo(1));
        Assert.That(loaded.Record.Episodes[0].WatchedAtUtc!.Value.Kind, Is.EqualTo(DateTimeKind.Utc));
        BsonDocument stored = _database.Read(db => db.GetCollection("anime").FindById(saved.Id));
        Assert.That(stored["Record"].AsDocument.ContainsKey("WatchedCount"), Is.False);
        loaded.Record.Episodes.Add(new() { EpisodeNumber = 7 });
        Assert.ThrowsAsync<AnimeValidationException>(() => _repository.UpdateAsync(loaded));
    }

    [Test]
    public async Task SameTitleIsAllowed_ButDuplicateExternalIdIsRejected()
    {
        await _repository.AddAsync(new() { Metadata = new() { Title = "Same title", BangumiId = "123" } });
        await _repository.AddAsync(new() { Metadata = new() { Title = "Same title" } });
        Assert.ThrowsAsync<AnimeConflictException>(() => _repository.AddAsync(new()
            { Metadata = new() { Title = "Other", BangumiId = "123" } }));
    }

    [Test]
    public async Task FutureSchemaIsRejected_WithoutResettingOrMigratingDatabase()
    {
        AnimeEntry entry = await _repository.AddAsync(new() { Metadata = new() { Title = "Future" } });
        _database.Write(db => { BsonDocument document = db.GetCollection("anime").FindById(entry.Id); document["SchemaVersion"] = 99; return db.GetCollection("anime").Update(document); });
        Assert.ThrowsAsync<InvalidDataException>(() => _repository.GetAllAsync());
        Assert.That(_database.Read(db => db.GetCollection("anime").FindById(entry.Id)["SchemaVersion"].AsInt32), Is.EqualTo(99));
        Assert.That(_database.Read(db => db.GetCollection("app_info").FindById("identity")["formatVersion"].AsInt32), Is.EqualTo(1));
    }

    [Test]
    public void ExternalCoverReference_IsRejectedAtRepositoryBoundary()
    {
        Assert.ThrowsAsync<AnimeValidationException>(() => _repository.AddAsync(new()
            { Metadata = new() { Title = "Unsafe", CoverRef = "C:/outside.png" } }));
    }

    [Test]
    public void UnsupportedDatabaseFormat_IsRejectedWithoutChangingExistingBytes()
    {
        _database.Write(db => { BsonDocument identity = db.GetCollection("app_info").FindById("identity"); identity["formatVersion"] = 99; return db.GetCollection("app_info").Update(identity); });
        _database.Dispose();
        byte[] before = File.ReadAllBytes(_paths.DatabasePath);
        using var next = new AppDatabaseService(_paths);
        Assert.Throws<InvalidDataException>(() => next.Initialize());
        Assert.That(File.ReadAllBytes(_paths.DatabasePath), Is.EqualTo(before));
    }
}
