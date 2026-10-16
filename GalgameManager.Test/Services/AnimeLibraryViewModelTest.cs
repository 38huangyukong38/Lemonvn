using LemonVN.ViewModels.Anime;
using GalgameManager.Contracts.Services;
using LemonVN.Application.Anime;
using LemonVN.Contracts.Services;
using LemonVN.Domain.Anime;
using LemonVN.Infrastructure.Anime;
using LemonVN.Services;
using LemonVN.ViewModels;
using Moq;

namespace GalgameManager.Test.Services;

[TestFixture]
public sealed class AnimeLibraryViewModelTest
{
    private AppDataPathService _paths = null!;
    private AppDatabaseService _database = null!;
    private IAnimeRepository _repository = null!;
    private AnimeCollectionService _collection = null!;
    private CoverStorageService _covers = null!;
    private AppLogService _log = null!;

    [SetUp]
    public void SetUp()
    {
        _paths = new(Path.Combine(TestEnvironmentSetup.Root, "M4Library", Guid.NewGuid().ToString("N")));
        _database = new(_paths);
        _database.Initialize();
        _repository = new LiteDbAnimeRepository(_database);
        _covers = new(_paths);
        _log = new(_paths);
        _collection = new(_repository, _covers, _log);
    }

    [TearDown] public void TearDown() => _database.Dispose();
    private AnimeLibraryViewModel Home() => new(new Mock<INavigationService>().Object, _collection, _covers, _log,
        new(), new Mock<IAnimeUiService>().Object, (key, args) => key + ":" + string.Join(",", args));

    [Test]
    public async Task CombinedFiltersAndClear_AffectOnlyPresentation()
    {
        await _collection.SaveAsync(new() { Metadata = new() { Title = "音乐 A" }, Record = new() { WatchStatus = WatchStatus.Watching, Tags = ["音乐"] } }, true);
        await _collection.SaveAsync(new() { Metadata = new() { Title = "音乐 B" }, Record = new() { WatchStatus = WatchStatus.Planned, Tags = ["音乐"] } }, true);
        AnimeLibraryViewModel home = Home();
        await home.ActivateAsync();
        home.StatusIndex = 2; // The independent library now starts with all entries.
        home.Search = "音乐";
        home.SelectedTag = "音乐";
        home.SortIndex = 3;
        Assert.That(home.Items.Single().Title, Is.EqualTo("音乐 A"));
        home.StatusIndex = 1;
        Assert.That(home.Items.Single().Title, Is.EqualTo("音乐 B"));
        home.Search = "No matches";
        Assert.That(home.HasNoMatches, Is.True);
        Assert.That(home.IsLibraryEmpty, Is.False);
        home.ClearFiltersCommand.Execute(null);
        Assert.That(home.Items, Has.Count.EqualTo(2));
        Assert.That(await _collection.GetAllAsync(), Has.Count.EqualTo(2));
    }

    [Test]
    public async Task TagEditingAndFailedSave_PreserveProgressRatingNotesAndDates()
    {
        AnimeEntry entry = await _collection.SaveAsync(new()
        {
            Metadata = new() { Title = "Tags and progress", TotalEpisodes = 12 },
            Record = new() { PersonalRating = 8, Notes = "keep", StartedAt = new(2024, 1, 1),
                Episodes = [new() { EpisodeNumber = 5, IsWatched = true, Notes = "episode note" }] }
        }, true);
        entry.Record.Tags = AnimeTagRules.Parse("音乐, music, MUSIC");
        entry = await _collection.SaveAsync(entry, false);
        Assert.That(entry.Record.Tags, Is.EqualTo(new[] { "音乐", "music" }));
        var failing = new Mock<IAnimeRepository>();
        failing.Setup(repo => repo.GetByIdAsync(entry.Id)).ReturnsAsync(() => entry.Copy());
        failing.Setup(repo => repo.UpdateAsync(It.IsAny<AnimeEntry>())).ThrowsAsync(new IOException("simulated failure"));
        var service = new AnimeCollectionService(failing.Object, _covers, _log);
        AnimeEntry draft = entry.Copy();
        draft.Record.Tags.Clear();
        Assert.ThrowsAsync<IOException>(() => service.SaveAsync(draft, false));
        AnimeEntry loaded = (await _collection.GetByIdAsync(entry.Id))!;
        Assert.That(loaded.Record.Tags, Is.EqualTo(entry.Record.Tags));
        Assert.That(loaded.Record.WatchedCount, Is.EqualTo(1));
        Assert.That(loaded.Record.PersonalRating, Is.EqualTo(8));
        Assert.That(loaded.Record.Notes, Is.EqualTo("keep"));
        Assert.That(loaded.Record.StartedAt, Is.EqualTo(new DateOnly(2024, 1, 1)));
        loaded.Record.Tags.Clear();
        loaded = await _collection.SaveAsync(loaded, false);
        Assert.That(loaded.Record.Tags, Is.Empty);
        Assert.That(loaded.Record.WatchedCount, Is.EqualTo(1));
    }

    [Test]
    public async Task EmptyAndSingleUnwatchedLibrary_HaveDistinctEmptyStates()
    {
        AnimeLibraryViewModel home = Home();
        await home.ActivateAsync();
        Assert.That(home.IsLibraryEmpty, Is.True);
        Assert.That(home.HasNoMatches, Is.False);
        await _collection.SaveAsync(new() { Metadata = new() { Title = "Only planned" } }, true);
        home = Home();
        await home.ActivateAsync();
        Assert.That(home.StatusIndex, Is.Zero);
        Assert.That(home.Items, Has.Count.EqualTo(1));
        Assert.That(home.Items[0].ProgressText, Is.EqualTo("AnimeProgress_Unknown:0"));
        Assert.That(home.Items[0].CoverSource, Is.EqualTo(CoverStorageService.Placeholder));
    }
}
