using LemonVN.Application.Anime;
using LemonVN.Contracts.Services;
using LemonVN.Domain.Anime;
using LemonVN.Infrastructure.Anime;
using LemonVN.Services;
using LemonVN.ViewModels.Anime;
using Moq;

namespace GalgameManager.Test.Services;

[TestFixture]
public sealed class AnimeProgressViewModelTest
{
    private AppDataPathService _paths = null!;
    private AppDatabaseService _database = null!;
    private IAnimeRepository _repository = null!;
    private AnimeCollectionService _collection = null!;
    private Mock<IAnimeUiService> _dialogs = null!;

    [SetUp]
    public void SetUp()
    {
        _paths = new(Path.Combine(TestEnvironmentSetup.Root, "ProgressUi", Guid.NewGuid().ToString("N")));
        _database = new(_paths);
        _database.Initialize();
        _repository = new LiteDbAnimeRepository(_database);
        _collection = new(_repository, new CoverStorageService(_paths), new AppLogService(_paths));
        _dialogs = new();
    }

    [TearDown]
    public void TearDown() => _database.Dispose();

    private AnimeProgressViewModel Create(AnimeCollectionService? collection = null)
    {
        collection ??= _collection;
        return new(new AnimeProgressService(collection), collection, _dialogs.Object, new AppLogService(_paths),
            (key, args) => key + ":" + string.Join(",", args));
    }

    private Task<AnimeEntry> Add(int? total = 12) => _collection.SaveAsync(new()
        { Metadata = new() { Title = "UI progress", TotalEpisodes = total } }, true);

    [Test]
    public async Task SavedCommands_SynchronizeCountRowsButtonsAndSummary()
    {
        AnimeEntry entry = await Add(3);
        AnimeProgressViewModel viewModel = Create();
        viewModel.Load(entry);
        Assert.That(viewModel.IsExpanded, Is.False);
        Assert.That(viewModel.EpisodeItems, Is.Empty);
        Assert.That(viewModel.CanMarkNext, Is.True);
        viewModel.IsExpanded = true;
        await viewModel.MarkNextCommand.ExecuteAsync(null);
        Assert.That(viewModel.WatchedCount, Is.EqualTo(1));
        Assert.That(viewModel.Summary, Is.EqualTo("AnimeProgress_Known:1,3"));
        Assert.That(viewModel.EpisodeItems.Single(item => item.EpisodeNumber == 1).IsWatched, Is.True);
        viewModel.ThroughText = "3";
        await viewModel.MarkThroughCommand.ExecuteAsync(null);
        Assert.That(viewModel.CanMarkNext, Is.False);
        Assert.That(viewModel.Percentage, Is.EqualTo(100));
        await viewModel.ToggleEpisodeCommand.ExecuteAsync(2);
        Assert.That(viewModel.WatchedCount, Is.EqualTo(2));
        Assert.That(viewModel.CanMarkNext, Is.True);
        Assert.That(viewModel.EpisodeItems.Single(item => item.EpisodeNumber == 2).IsWatched, Is.False);
        Assert.That((await _collection.GetByIdAsync(entry.Id))!.Record.WatchedCount, Is.EqualTo(2));
    }

    [Test]
    public async Task WriteFailure_LeavesVisibleStatePersisted_AndShowsError()
    {
        AnimeEntry entry = await Add();
        var failing = new Mock<IAnimeRepository>();
        failing.Setup(repo => repo.GetByIdAsync(entry.Id)).ReturnsAsync(() => entry.Copy());
        failing.Setup(repo => repo.UpdateAsync(It.IsAny<AnimeEntry>())).ThrowsAsync(new IOException("simulated disk failure"));
        var collection = new AnimeCollectionService(failing.Object, new CoverStorageService(_paths), new AppLogService(_paths));
        AnimeProgressViewModel viewModel = Create(collection);
        viewModel.Load(entry);
        viewModel.IsExpanded = true;
        int savedEvents = 0;
        viewModel.Saved += _ => savedEvents++;
        await viewModel.MarkNextCommand.ExecuteAsync(null);
        Assert.That(viewModel.IsErrorOpen, Is.True);
        Assert.That(viewModel.ErrorMessage, Does.StartWith("AnimeProgress_SaveFailed:"));
        Assert.That(viewModel.WatchedCount, Is.Zero);
        Assert.That(viewModel.EpisodeItems.Any(item => item.IsWatched), Is.False);
        Assert.That(savedEvents, Is.Zero);
        Assert.That(viewModel.IsBusy, Is.False);
    }

    [Test]
    public async Task StaleRevision_ReloadsLatest_AndDoesNotOverwriteAnotherChange()
    {
        AnimeEntry entry = await Add();
        AnimeProgressViewModel viewModel = Create();
        viewModel.Load(entry);
        var progress = new AnimeProgressService(_collection);
        await progress.SetEpisodeWatchedAsync(entry.Id, entry.Revision, 5, true);
        await viewModel.MarkNextCommand.ExecuteAsync(null);
        Assert.That(viewModel.WatchedCount, Is.EqualTo(1));
        Assert.That(viewModel.ErrorMessage, Is.EqualTo("AnimeProgress_Conflict:"));
        Assert.That((await _collection.GetByIdAsync(entry.Id))!.Record.Episodes.Single().EpisodeNumber, Is.EqualTo(5));
        await viewModel.MarkNextCommand.ExecuteAsync(null);
        Assert.That(viewModel.WatchedCount, Is.EqualTo(2));
        Assert.That(viewModel.IsErrorOpen, Is.False);
    }

    [Test]
    public async Task BusyState_RejectsAnotherCommand_AndNeverShowsUncommittedCount()
    {
        AnimeEntry entry = await Add();
        var gate = new TaskCompletionSource<AnimeEntry>(TaskCreationOptions.RunContinuationsAsynchronously);
        var delayed = new Mock<IAnimeRepository>();
        delayed.Setup(repo => repo.GetByIdAsync(entry.Id)).ReturnsAsync(() => entry.Copy());
        delayed.Setup(repo => repo.UpdateAsync(It.IsAny<AnimeEntry>())).Returns(gate.Task);
        var collection = new AnimeCollectionService(delayed.Object, new CoverStorageService(_paths), new AppLogService(_paths));
        AnimeProgressViewModel viewModel = Create(collection);
        viewModel.Load(entry);
        Task pending = viewModel.MarkNextCommand.ExecuteAsync(null);
        Assert.That(viewModel.IsBusy, Is.True);
        Assert.That(viewModel.CanInteract, Is.False);
        Assert.That(viewModel.WatchedCount, Is.Zero);
        await viewModel.ToggleEpisodeCommand.ExecuteAsync(5);
        delayed.Verify(repo => repo.UpdateAsync(It.IsAny<AnimeEntry>()), Times.Once);
        AnimeEntry saved = entry.Copy();
        saved.Revision++;
        saved.Record.Episodes.Add(new() { EpisodeNumber = 1, IsWatched = true });
        gate.SetResult(saved);
        await pending;
        Assert.That(viewModel.WatchedCount, Is.EqualTo(1));
        Assert.That(viewModel.IsBusy, Is.False);
    }

    [Test]
    public async Task SkippedBatch_RequiresConfirmation_AndCancelDoesNotWrite()
    {
        AnimeEntry entry = await Add();
        var progress = new AnimeProgressService(_collection);
        entry = await progress.SetEpisodeWatchedAsync(entry.Id, entry.Revision, 5, true);
        AnimeProgressViewModel viewModel = Create();
        viewModel.Load(entry);
        viewModel.ThroughText = "8";
        _dialogs.Setup(dialog => dialog.ConfirmMarkThroughAsync(8)).ReturnsAsync(false);
        await viewModel.MarkThroughCommand.ExecuteAsync(null);
        Assert.That(viewModel.WatchedCount, Is.EqualTo(1));
        Assert.That((await _collection.GetByIdAsync(entry.Id))!.Revision, Is.EqualTo(entry.Revision));
        _dialogs.Setup(dialog => dialog.ConfirmMarkThroughAsync(8)).ReturnsAsync(true);
        await viewModel.MarkThroughCommand.ExecuteAsync(null);
        Assert.That(viewModel.WatchedCount, Is.EqualTo(8));
    }

    [TestCase("0")]
    [TestCase("-1")]
    [TestCase("12.5")]
    [TestCase("2147483648")]
    [TestCase("10001")]
    [TestCase("13")]
    public async Task InvalidBatchText_ShowsErrorWithoutChangingCount(string text)
    {
        AnimeEntry entry = await Add();
        AnimeProgressViewModel viewModel = Create();
        viewModel.Load(entry);
        viewModel.ThroughText = text;
        await viewModel.MarkThroughCommand.ExecuteAsync(null);
        Assert.That(viewModel.IsErrorOpen, Is.True);
        Assert.That(viewModel.WatchedCount, Is.Zero);
        Assert.That((await _collection.GetByIdAsync(entry.Id))!.Revision, Is.EqualTo(entry.Revision));
    }

    [Test]
    public async Task PagedRowsAndUnknownSummary_AreBoundedAndDerived()
    {
        AnimeEntry entry = await Add(120);
        AnimeProgressViewModel viewModel = Create();
        viewModel.Load(entry);
        viewModel.IsExpanded = true;
        Assert.That(viewModel.EpisodeItems, Has.Count.EqualTo(50));
        viewModel.NextPageCommand.Execute(null);
        Assert.That(viewModel.EpisodeItems[0].EpisodeNumber, Is.EqualTo(51));
        viewModel.PageStartText = "120";
        viewModel.JumpPageCommand.Execute(null);
        Assert.That(viewModel.EpisodeItems, Has.Count.EqualTo(1));
        Assert.That(viewModel.CanNextPage, Is.False);
        entry.Metadata.TotalEpisodes = null;
        entry = await _collection.SaveAsync(entry, false);
        viewModel.Load(entry);
        Assert.That(viewModel.HasKnownTotal, Is.False);
        Assert.That(viewModel.Summary, Is.EqualTo("AnimeProgress_Unknown:0"));
        Assert.That(viewModel.EpisodeItems, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task ReducedTotal_ShowsWarningAndActualCount_WithoutTruncatingEpisodeRows()
    {
        AnimeEntry entry = await Add();
        entry = await new AnimeProgressService(_collection).MarkWatchedThroughAsync(entry.Id, entry.Revision, 12);
        entry.Metadata.TotalEpisodes = 10;
        entry = await _collection.SaveAsync(entry, false);
        AnimeProgressViewModel viewModel = Create();
        viewModel.Load(entry);
        viewModel.IsExpanded = true;
        Assert.That(viewModel.HasOutOfRange, Is.True);
        Assert.That(viewModel.OutOfRangeMessage, Is.EqualTo("AnimeProgress_Outside:2"));
        Assert.That(viewModel.Summary, Is.EqualTo("AnimeProgress_Known:12,10"));
        Assert.That(viewModel.EpisodeItems, Has.Count.EqualTo(12));
        Assert.That(viewModel.CanMarkNext, Is.False);
        Assert.That(viewModel.UndoLabel, Is.EqualTo("AnimeProgress_Undo:12"));
    }
}
