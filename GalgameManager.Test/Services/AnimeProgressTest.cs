using LemonVN.Application.Anime;
using LemonVN.Contracts.Services;
using LemonVN.Domain.Anime;
using LemonVN.Infrastructure.Anime;
using LemonVN.Services;
using Moq;

namespace GalgameManager.Test.Services;

[TestFixture]
public sealed class AnimeProgressTest
{
    private AppDataPathService _paths = null!;
    private AppDatabaseService _database = null!;
    private IAnimeRepository _repository = null!;
    private AnimeCollectionService _collection = null!;
    private AnimeProgressService _progress = null!;

    [SetUp]
    public void SetUp()
    {
        _paths = new(Path.Combine(TestEnvironmentSetup.Root, "Progress", Guid.NewGuid().ToString("N")));
        Open();
    }

    private void Open()
    {
        _database = new(_paths);
        _database.Initialize();
        _repository = new LiteDbAnimeRepository(_database);
        _collection = new(_repository, new CoverStorageService(_paths), new AppLogService(_paths));
        _progress = new(_collection);
    }

    [TearDown]
    public void TearDown() => _database.Dispose();

    private Task<AnimeEntry> Add(int? total = 12) => _collection.SaveAsync(new()
    {
        Metadata = new() { Title = "Offline progress", TotalEpisodes = total },
        Record = new()
        {
            WatchStatus = WatchStatus.OnHold, PersonalRating = 9, Notes = "Keep personal notes",
            Tags = ["Favourite"], StartedAt = new(2024, 4, 1), CompletedAt = new(2024, 6, 1)
        }
    }, true);

    [Test]
    public async Task Empty_FirstEpisode_AndContiguousProgress_AreDerived()
    {
        AnimeEntry entry = await Add();
        Assert.That(_progress.GetProgress(entry).WatchedCount, Is.Zero);
        Assert.That(_progress.GetProgress(entry).HighestWatched, Is.Null);
        entry = await _progress.MarkNextUnwatchedAsync(entry.Id, entry.Revision);
        Assert.That(entry.Record.WatchedCount, Is.EqualTo(1));
        entry = await _progress.MarkWatchedThroughAsync(entry.Id, entry.Revision, 8);
        AnimeProgressState state = _progress.GetProgress(entry);
        Assert.That(state.WatchedCount, Is.EqualTo(8));
        Assert.That(state.Percentage, Is.EqualTo(800.0 / 12));
        Assert.That(state.NextUnwatched, Is.EqualTo(9));
        Assert.That(entry.Record.Episodes.Select(episode => episode.EpisodeNumber), Is.EquivalentTo(Enumerable.Range(1, 8)));
    }

    [Test]
    public async Task Skips_CountWatchedEpisodes_AndNextFillsSmallestGap()
    {
        AnimeEntry entry = await Add();
        foreach (int number in new[] { 1, 2, 4, 5 })
            entry = await _progress.SetEpisodeWatchedAsync(entry.Id, entry.Revision, number, true);
        Assert.That(_progress.GetProgress(entry).WatchedCount, Is.EqualTo(4));
        Assert.That(_progress.GetProgress(entry).Percentage, Is.EqualTo(100.0 / 3));
        Assert.That(_progress.GetEpisodeStates(entry).Single(episode => episode.EpisodeNumber == 3).IsWatched, Is.False);
        entry = await _progress.MarkNextUnwatchedAsync(entry.Id, entry.Revision);
        Assert.That(entry.Record.Episodes.Single(episode => episode.EpisodeNumber == 3).IsWatched, Is.True);
        Assert.That(entry.Record.WatchedCount, Is.EqualTo(5));
    }

    [Test]
    public async Task Batch_IsIdempotent_AndPreservesLaterRecordsAndEpisodeNotes()
    {
        AnimeEntry entry = await Add();
        entry.Record.Episodes = [new() { EpisodeNumber = 2, Notes = "episode note" },
            new() { EpisodeNumber = 10, IsWatched = true, WatchedAtUtc = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), Notes = "later note" }];
        entry = await _collection.UpdateRecordAsync(entry.Id, entry.Revision, entry.Record);
        entry = await _progress.MarkWatchedThroughAsync(entry.Id, entry.Revision, 8);
        AnimeEntry repeated = await _progress.MarkWatchedThroughAsync(entry.Id, entry.Revision, 8);
        Assert.That(repeated.Revision, Is.EqualTo(entry.Revision));
        Assert.That(repeated.Record.WatchedCount, Is.EqualTo(9));
        Assert.That(repeated.Record.Episodes.Single(episode => episode.EpisodeNumber == 2).Notes, Is.EqualTo("episode note"));
        Assert.That(repeated.Record.Episodes.Single(episode => episode.EpisodeNumber == 10).Notes, Is.EqualTo("later note"));
        Assert.That(repeated.Record.Episodes.Single(episode => episode.EpisodeNumber == 10).WatchedAtUtc,
            Is.EqualTo(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        Assert.That(repeated.Record.Episodes.Select(episode => episode.EpisodeNumber).Distinct().Count(), Is.EqualTo(9));
    }

    [Test]
    public async Task UnwatchAndHighestUndo_ClearDate_PreserveNotes_AndRemainSparse()
    {
        AnimeEntry entry = await Add();
        entry.Record.Episodes = [new() { EpisodeNumber = 3, Notes = "retain" }];
        entry = await _collection.UpdateRecordAsync(entry.Id, entry.Revision, entry.Record);
        entry = await _progress.SetEpisodeWatchedAsync(entry.Id, entry.Revision, 3, true);
        Assert.That(entry.Record.Episodes[0].WatchedAtUtc!.Value.Kind, Is.EqualTo(DateTimeKind.Utc));
        entry = await _progress.UnmarkHighestWatchedAsync(entry.Id, entry.Revision);
        Assert.That(entry.Record.Episodes[0].IsWatched, Is.False);
        Assert.That(entry.Record.Episodes[0].WatchedAtUtc, Is.Null);
        Assert.That(entry.Record.Episodes[0].Notes, Is.EqualTo("retain"));
        AnimeEntry repeated = await _progress.SetEpisodeWatchedAsync(entry.Id, entry.Revision, 3, false);
        Assert.That(repeated.Revision, Is.EqualTo(entry.Revision));
        repeated = await _progress.SetEpisodeWatchedAsync(entry.Id, repeated.Revision, 7, false);
        Assert.That(repeated.Record.Episodes, Has.Count.EqualTo(1));
        Assert.That((await _progress.UnmarkHighestWatchedAsync(entry.Id, repeated.Revision)).Revision, Is.EqualTo(entry.Revision));
    }

    [Test]
    public async Task KnownTotal_AllWatched_HasNoNext_AndStatusRemainsIndependent()
    {
        AnimeEntry entry = await Add(3);
        entry = await _progress.MarkWatchedThroughAsync(entry.Id, entry.Revision, 3);
        Assert.That(_progress.GetProgress(entry).NextUnwatched, Is.Null);
        Assert.That(entry.Record.WatchStatus, Is.EqualTo(WatchStatus.OnHold));
        Assert.ThrowsAsync<AnimeValidationException>(() => _progress.MarkNextUnwatchedAsync(entry.Id, entry.Revision));
        Assert.ThrowsAsync<AnimeValidationException>(() => _progress.SetEpisodeWatchedAsync(entry.Id, entry.Revision, 4, true));
        Assert.That((await _collection.GetByIdAsync(entry.Id))!.Revision, Is.EqualTo(entry.Revision));
    }

    [Test]
    public async Task UnknownTotal_ContinuesNaturally_WithoutAssumingTwelve()
    {
        AnimeEntry entry = await Add(null);
        Assert.That(_progress.GetEpisodeStates(entry), Has.Count.EqualTo(1));
        for (int number = 1; number <= 15; number++) entry = await _progress.MarkNextUnwatchedAsync(entry.Id, entry.Revision);
        Assert.That(_progress.GetProgress(entry).TotalEpisodes, Is.Null);
        Assert.That(_progress.GetProgress(entry).WatchedCount, Is.EqualTo(15));
        Assert.That(_progress.GetProgress(entry).NextUnwatched, Is.EqualTo(16));
    }

    [Test]
    public async Task ReducedTotal_PreservesOutsideRecords_AndClampsBarWithoutHidingCount()
    {
        AnimeEntry entry = await Add();
        entry = await _progress.MarkWatchedThroughAsync(entry.Id, entry.Revision, 12);
        entry.Metadata.TotalEpisodes = 10;
        entry = await _collection.SaveAsync(entry, false);
        AnimeProgressState state = _progress.GetProgress(entry);
        Assert.That(state.WatchedCount, Is.EqualTo(12));
        Assert.That(state.OutOfRangeCount, Is.EqualTo(2));
        Assert.That(state.Percentage, Is.EqualTo(100));
        Assert.That(state.NextUnwatched, Is.Null);
        Assert.That(_progress.GetEpisodeStates(entry).Single(episode => episode.EpisodeNumber == 12).IsOutOfRange, Is.True);
        entry = await _progress.SetEpisodeWatchedAsync(entry.Id, entry.Revision, 12, false);
        Assert.That(entry.Record.Episodes, Has.Count.EqualTo(12));
        Assert.That(entry.Record.WatchedCount, Is.EqualTo(11));
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(10_001)]
    [TestCase(int.MaxValue)]
    public async Task InvalidOrExtremeBatch_IsRejectedWithoutWriting(int number)
    {
        AnimeEntry entry = await Add(null);
        Assert.ThrowsAsync<AnimeValidationException>(() => _progress.MarkWatchedThroughAsync(entry.Id, entry.Revision, number));
        Assert.That((await _collection.GetByIdAsync(entry.Id))!.Revision, Is.EqualTo(entry.Revision));
    }

    [Test]
    public async Task BatchAboveKnownTotal_IsRejected()
    {
        AnimeEntry entry = await Add(3);
        Assert.ThrowsAsync<AnimeValidationException>(() => _progress.MarkWatchedThroughAsync(entry.Id, entry.Revision, 4));
        Assert.ThrowsAsync<AnimeValidationException>(() => _progress.SetEpisodeWatchedAsync(entry.Id, entry.Revision, 0, true));
        Assert.That((await _collection.GetByIdAsync(entry.Id))!.Record.Episodes, Is.Empty);
    }

    [Test]
    public async Task MaximumBatch_IsSupported_AndOnlyFiftyRowsAreProjected()
    {
        AnimeEntry entry = await Add(null);
        entry = await _progress.MarkWatchedThroughAsync(entry.Id, entry.Revision, AnimeProgressService.MaxBatchEpisodes);
        Assert.That(entry.Record.WatchedCount, Is.EqualTo(10_000));
        Assert.That(_progress.GetEpisodeStates(entry), Has.Count.EqualTo(50));
        Assert.That((await _collection.GetByIdAsync(entry.Id))!.Record.Episodes, Has.Count.EqualTo(10_000));
    }

    [Test]
    public async Task ChangeBetweenReadAndCommit_IsRejectedByM2RevisionProtection()
    {
        AnimeEntry entry = await Add();
        var racing = new Mock<IAnimeRepository>();
        int reads = 0;
        racing.Setup(repo => repo.GetByIdAsync(entry.Id)).Returns(async () =>
        {
            AnimeEntry loaded = (await _repository.GetByIdAsync(entry.Id))!;
            if (++reads == 1)
            {
                AnimeEntry newer = loaded.Copy();
                newer.Record.Notes = "newer personal note";
                await _repository.UpdateAsync(newer);
            }
            return loaded;
        });
        racing.Setup(repo => repo.UpdateAsync(It.IsAny<AnimeEntry>()))
            .Returns<AnimeEntry>(value => _repository.UpdateAsync(value));
        var collection = new AnimeCollectionService(racing.Object, new CoverStorageService(_paths), new AppLogService(_paths));
        var progress = new AnimeProgressService(collection);
        Assert.ThrowsAsync<AnimeConflictException>(() => progress.MarkNextUnwatchedAsync(entry.Id, entry.Revision));
        AnimeEntry preserved = (await _repository.GetByIdAsync(entry.Id))!;
        Assert.That(preserved.Record.Notes, Is.EqualTo("newer personal note"));
        Assert.That(preserved.Record.WatchedCount, Is.Zero);
    }

    [Test]
    public async Task HugeEpisodeRange_IsPaged_AndProjectionCreatesNoRecords()
    {
        AnimeEntry entry = await Add(int.MaxValue);
        Assert.That(_progress.GetEpisodeStates(entry), Has.Count.EqualTo(AnimeProgressService.PageSize));
        Assert.That(_progress.GetEpisodeStates(entry, int.MaxValue).Single().EpisodeNumber, Is.EqualTo(int.MaxValue));
        Assert.That(entry.Record.Episodes, Is.Empty);
        Assert.That((await _collection.GetByIdAsync(entry.Id))!.Record.Episodes, Is.Empty);
        entry = await _progress.SetEpisodeWatchedAsync(entry.Id, entry.Revision, int.MaxValue, true);
        entry.Metadata.TotalEpisodes = null;
        entry = await _collection.SaveAsync(entry, false);
        Assert.That(_progress.GetProgress(entry).LastDisplayEpisode, Is.EqualTo(int.MaxValue));
        Assert.That(_progress.GetProgress(entry).NextUnwatched, Is.EqualTo(1));
    }

    [Test]
    public async Task Reopen_KeepsProgressDatesPersonalFieldsAndMetadata()
    {
        AnimeEntry entry = await Add();
        entry = await _progress.MarkWatchedThroughAsync(entry.Id, entry.Revision, 8);
        entry = await _progress.SetEpisodeWatchedAsync(entry.Id, entry.Revision, 3, false);
        _database.Dispose();
        Open();
        AnimeEntry loaded = (await _collection.GetByIdAsync(entry.Id))!;
        Assert.That(_progress.GetProgress(loaded).WatchedCount, Is.EqualTo(7));
        Assert.That(loaded.Record.Episodes.Single(episode => episode.EpisodeNumber == 3).WatchedAtUtc, Is.Null);
        Assert.That(loaded.Record.Episodes.Single(episode => episode.EpisodeNumber == 5).WatchedAtUtc!.Value.Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(loaded.Record.WatchStatus, Is.EqualTo(WatchStatus.OnHold));
        Assert.That(loaded.Record.PersonalRating, Is.EqualTo(9));
        Assert.That(loaded.Record.Notes, Is.EqualTo("Keep personal notes"));
        Assert.That(loaded.Record.Tags, Is.EqualTo(new[] { "Favourite" }));
        Assert.That(loaded.Record.StartedAt, Is.EqualTo(new DateOnly(2024, 4, 1)));
        Assert.That(loaded.Record.CompletedAt, Is.EqualTo(new DateOnly(2024, 6, 1)));
        Assert.That(loaded.Metadata.Title, Is.EqualTo("Offline progress"));
        Assert.That(loaded.Metadata.TotalEpisodes, Is.EqualTo(12));
    }

    [Test]
    public async Task RapidConcurrentCommands_RejectStaleRevision_WithoutDuplicatesOrLostWrites()
    {
        AnimeEntry entry = await Add();
        Task<AnimeEntry>[] pending = Enumerable.Range(0, 5)
            .Select(_ => _progress.MarkNextUnwatchedAsync(entry.Id, entry.Revision)).ToArray();
        foreach (Task<AnimeEntry> task in pending)
        {
            try { await task; }
            catch (AnimeConflictException) { }
        }
        Assert.That(pending.Count(task => task.IsCompletedSuccessfully), Is.EqualTo(1));
        Assert.That(pending.Count(task => task.Exception?.InnerException is AnimeConflictException), Is.EqualTo(4));
        AnimeEntry loaded = (await _collection.GetByIdAsync(entry.Id))!;
        Assert.That(loaded.Record.Episodes, Has.Count.EqualTo(1));
        Assert.That(loaded.Revision, Is.EqualTo(entry.Revision + 1));
    }

    [Test]
    public async Task DatabaseFailure_DoesNotMutateTheLoadedRecordOrPersistProgress()
    {
        AnimeEntry entry = await Add();
        var repository = new Mock<IAnimeRepository>();
        repository.Setup(service => service.GetByIdAsync(entry.Id)).ReturnsAsync(() => entry.Copy());
        repository.Setup(service => service.UpdateAsync(It.IsAny<AnimeEntry>())).ThrowsAsync(new IOException("simulated write failure"));
        var collection = new AnimeCollectionService(repository.Object, new CoverStorageService(_paths), new AppLogService(_paths));
        var progress = new AnimeProgressService(collection);
        Assert.ThrowsAsync<IOException>(() => progress.MarkNextUnwatchedAsync(entry.Id, entry.Revision));
        Assert.That(entry.Record.Episodes, Is.Empty);
        Assert.That((await _collection.GetByIdAsync(entry.Id))!.Record.Episodes, Is.Empty);
    }
}
