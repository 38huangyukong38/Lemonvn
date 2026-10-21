using System.Text.Json;
using LemonVN.Application.Anime;
using LemonVN.Contracts.Services;
using LemonVN.Domain.Anime;
using LemonVN.Infrastructure.Anime;
using LemonVN.Services;
using LemonVN.ViewModels.Anime;

namespace GalgameManager.Test.Services;

[TestFixture]
public sealed class AnimeAutomaticStatusTest
{
    [TestCase(12, 0, WatchStatus.Planned)]
    [TestCase(12, 1, WatchStatus.Watching)]
    [TestCase(12, 11, WatchStatus.Watching)]
    [TestCase(12, 12, WatchStatus.Completed)]
    [TestCase(null, 0, WatchStatus.Planned)]
    [TestCase(null, 12, WatchStatus.Watching)]
    public void DerivedStatus_UsesOnlyWatchedRegularEpisodes_WithoutMutatingOldRecord(int? total, int watched, WatchStatus expected)
    {
        var entry = new AnimeEntry
        {
            Metadata = new() { Title = "Status fixture", TotalEpisodes = total },
            Record = new() { WatchStatus = WatchStatus.Watching,
                Episodes = Enumerable.Range(1, watched).Select(n => new EpisodeRecord { EpisodeNumber = n, IsWatched = true }).ToList() }
        };
        string before = JsonSerializer.Serialize(entry);
        Assert.That(AnimeWatchStatusRules.GetStatus(entry), Is.EqualTo(expected));
        Assert.That(JsonSerializer.Serialize(entry), Is.EqualTo(before));
    }

    [Test]
    public void OutOfRangeEpisodes_CannotFillUnwatchedGapsOrPretendCompletion()
    {
        var entry = new AnimeEntry { Metadata = new() { Title = "Gaps", TotalEpisodes = 3 },
            Record = new() { Episodes = new[] { 2, 3, 4 }.Select(n => new EpisodeRecord { EpisodeNumber = n, IsWatched = true }).ToList() } };
        Assert.That(entry.Record.WatchedCount, Is.EqualTo(3));
        Assert.That(AnimeWatchStatusRules.GetStatus(entry), Is.EqualTo(WatchStatus.Watching));
        entry.Record.Episodes.Add(new() { EpisodeNumber = 1, IsWatched = true });
        Assert.That(AnimeWatchStatusRules.GetStatus(entry), Is.EqualTo(WatchStatus.Completed));
        entry.Record.Episodes.Single(e => e.EpisodeNumber == 2).IsWatched = false;
        Assert.That(AnimeWatchStatusRules.GetStatus(entry), Is.EqualTo(WatchStatus.Watching));
    }

    [TestCase(WatchStatus.OnHold)]
    [TestCase(WatchStatus.Dropped)]
    public async Task ManualOverride_SurvivesProgressAndRestart_AndCanReturnToAutomatic(WatchStatus manual)
    {
        var paths = new AppDataPathService(Path.Combine(TestEnvironmentSetup.Root, "AutomaticStatus", Guid.NewGuid().ToString("N")));
        Guid id;
        using (var database = new AppDatabaseService(paths))
        using (var log = new AppLogService(paths))
        {
            database.Initialize();
            var collection = new AnimeCollectionService(new LiteDbAnimeRepository(database), new CoverStorageService(paths), log);
            var progress = new AnimeProgressService(collection);
            AnimeEntry entry = await collection.SaveAsync(new() { Metadata = new() { Title = "Manual exception", TotalEpisodes = 2 },
                Record = new() { WatchStatus = manual, PersonalRating = 8, Notes = "keep", Tags = ["music"] } }, true);
            entry = await progress.MarkWatchedThroughAsync(entry.Id, entry.Revision, 2);
            Assert.That(entry.Record.WatchStatus, Is.EqualTo(manual));
            entry.Record.WatchStatus = WatchStatus.Planned; // Explicitly return to automatic; do not alter Episodes.
            entry = await collection.UpdateRecordAsync(entry.Id, entry.Revision, entry.Record);
            Assert.That(entry.Record.WatchStatus, Is.EqualTo(WatchStatus.Completed));
            id = entry.Id;
        }
        using var reopened = new AppDatabaseService(paths); reopened.Initialize();
        AnimeEntry loaded = (await new LiteDbAnimeRepository(reopened).GetByIdAsync(id))!;
        Assert.That(loaded.Record.WatchStatus, Is.EqualTo(WatchStatus.Completed));
        Assert.That(loaded.Record.WatchedCount, Is.EqualTo(2));
        Assert.That(loaded.Record.PersonalRating, Is.EqualTo(8));
        Assert.That(loaded.Record.Notes, Is.EqualTo("keep"));
        Assert.That(loaded.Record.Tags, Is.EqualTo(new[] { "music" }));
    }

    [Test]
    public async Task ProgressAutomaticallyTransitions_PlannedWatchingCompletedAndBack_WithoutChangingPersonalFields()
    {
        var paths = new AppDataPathService(Path.Combine(TestEnvironmentSetup.Root, "AutomaticProgress", Guid.NewGuid().ToString("N")));
        using var database = new AppDatabaseService(paths); database.Initialize();
        using var log = new AppLogService(paths);
        var repository = new LiteDbAnimeRepository(database);
        var collection = new AnimeCollectionService(repository, new CoverStorageService(paths), log);
        var progress = new AnimeProgressService(collection);
        AnimeEntry entry = await collection.SaveAsync(new() { Metadata = new() { Title = "New", TotalEpisodes = 2 },
            Record = new() { Notes = "personal", PersonalRating = 9, Tags = ["favourite"], StartedAt = new(2024, 1, 1) } }, true);
        Assert.That(entry.Record.WatchStatus, Is.EqualTo(WatchStatus.Planned));
        entry = await progress.MarkNextUnwatchedAsync(entry.Id, entry.Revision);
        Assert.That(entry.Record.WatchStatus, Is.EqualTo(WatchStatus.Watching));
        entry = await progress.MarkWatchedThroughAsync(entry.Id, entry.Revision, 2);
        Assert.That(entry.Record.WatchStatus, Is.EqualTo(WatchStatus.Completed));
        long revision = entry.Revision;
        Assert.ThrowsAsync<AnimeConflictException>(() => progress.UnmarkHighestWatchedAsync(entry.Id, revision - 1));
        Assert.That((await repository.GetByIdAsync(entry.Id))!.Record.WatchStatus, Is.EqualTo(WatchStatus.Completed));
        entry = await progress.UnmarkHighestWatchedAsync(entry.Id, revision);
        Assert.That(entry.Record.WatchStatus, Is.EqualTo(WatchStatus.Watching));
        entry = await progress.UnmarkHighestWatchedAsync(entry.Id, entry.Revision);
        Assert.That(entry.Record.WatchStatus, Is.EqualTo(WatchStatus.Planned));
        Assert.That(entry.Record.Notes, Is.EqualTo("personal"));
        Assert.That(entry.Record.PersonalRating, Is.EqualTo(9));
        Assert.That(entry.Record.Tags, Is.EqualTo(new[] { "favourite" }));
        Assert.That(entry.Record.StartedAt, Is.EqualTo(new DateOnly(2024, 1, 1)));
        Assert.That(entry.Record.Episodes, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task LegacyCompletedProgress_IsDisplayedAndFilteredCorrectly_WithoutReadTimeWrites()
    {
        var paths = new AppDataPathService(Path.Combine(TestEnvironmentSetup.Root, "LegacyStatus", Guid.NewGuid().ToString("N")));
        using var database = new AppDatabaseService(paths); database.Initialize();
        var repository = new LiteDbAnimeRepository(database);
        AnimeEntry entry = await repository.AddAsync(new() { Metadata = new() { Title = "Legacy", TotalEpisodes = 2 },
            Record = new() { WatchStatus = WatchStatus.Watching, Episodes = [new() { EpisodeNumber = 1, IsWatched = true }, new() { EpisodeNumber = 2, IsWatched = true }] } });
        entry = (await repository.GetByIdAsync(entry.Id))!; // Compare persisted LiteDB millisecond precision.
        string before = JsonSerializer.Serialize(entry);
        var query = new AnimeQueryService();
        Assert.That(query.Apply(new[] { entry }, new(Status: WatchStatus.Completed)), Has.Count.EqualTo(1));
        Assert.That(query.Apply(new[] { entry }, new(Status: WatchStatus.Watching)), Is.Empty);
        var card = new AnimeCardViewModel(entry, new CoverStorageService(paths), (key, _) => key);
        Assert.That(card.Target.Status, Is.EqualTo(WatchStatus.Completed));
        Assert.That(card.Status, Is.EqualTo("Anime_Status_Completed"));
        Assert.That(JsonSerializer.Serialize(await repository.GetByIdAsync(entry.Id)), Is.EqualTo(before));
    }
}
