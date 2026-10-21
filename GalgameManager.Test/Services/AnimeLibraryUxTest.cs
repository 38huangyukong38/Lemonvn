using System.Text.Json;
using System.Xml.Linq;
using GalgameManager.Contracts.Services;
using LemonVN.Application.Anime;
using LemonVN.Contracts.Services;
using LemonVN.Domain.Anime;
using LemonVN.Infrastructure.Anime;
using LemonVN.Services;
using LemonVN.ViewModels;
using LemonVN.ViewModels.Anime;
using LemonVN.Views;
using LemonVN.Views.Anime;
using Moq;

namespace GalgameManager.Test.Services;

[TestFixture]
public sealed class AnimeLibraryUxTest
{
    private AppDatabaseService _database = null!;
    private IAnimeRepository _repository = null!;
    private AnimeCollectionService _collection = null!;
    private CoverStorageService _covers = null!;
    private AppLogService _log = null!;
    private Mock<INavigationService> _navigation = null!;
    private Mock<IAnimeUiService> _dialogs = null!;
    private static string Text(string key, object[] args) => key + ":" + string.Join(",", args);

    [SetUp]
    public void SetUp()
    {
        var paths = new AppDataPathService(Path.Combine(TestEnvironmentSetup.Root, "M55Ux", Guid.NewGuid().ToString("N")));
        _database = new(paths); _database.Initialize();
        _repository = new LiteDbAnimeRepository(_database); _covers = new(paths); _log = new(paths);
        _collection = new(_repository, _covers, _log); _navigation = new(); _dialogs = new();
    }
    [TearDown] public void TearDown() { _database.Dispose(); _log.Dispose(); }
    private AnimeLibraryViewModel ViewModel(AnimeCollectionService? collection = null) =>
        new(_navigation.Object, collection ?? _collection, _covers, _log, new(), _dialogs.Object, Text);
    private async Task<AnimeEntry> Add(string title = "A", WatchStatus status = WatchStatus.Watching, string? bangumiId = null)
    {
        var saved = await _collection.SaveAsync(new()
        {
            Metadata = new() { Title = title, OriginalTitle = "Original " + title, TotalEpisodes = 12, BangumiId = bangumiId },
            Record = new() { WatchStatus = status, PersonalRating = 9, Tags = ["Music"], Notes = "Personal note",
                StartedAt = new(2024, 1, 2), CompletedAt = new(2024, 2, 3),
                Episodes = [new() { EpisodeNumber = 5, IsWatched = true, WatchedAtUtc = new(2024, 1, 3, 0, 0, 0, DateTimeKind.Utc), Notes = "Keep episode" }] }
        }, true);
        return (await _collection.GetByIdAsync(saved.Id))!;
    }

    [TestCase(WatchStatus.Planned)]
    [TestCase(WatchStatus.Watching)]
    [TestCase(WatchStatus.Completed)]
    [TestCase(WatchStatus.OnHold)]
    [TestCase(WatchStatus.Dropped)]
    public async Task StatusMenu_ChangesOnlyWatchStatus_AndRepeatedSelectionIsNoOp(WatchStatus status)
    {
        AnimeEntry before = await Add();
        var vm = ViewModel(); await vm.ActivateAsync();
        AnimeCardTarget target = vm.Items.Single().Target;
        await vm.ChangeStatusCommand.ExecuteAsync(new AnimeStatusChange(target, status));
        AnimeEntry saved = (await _collection.GetByIdAsync(before.Id))!;
        Assert.That(JsonSerializer.Serialize(saved.Metadata), Is.EqualTo(JsonSerializer.Serialize(before.Metadata)));
        AnimeEntry requested = before.Copy(); requested.Record.WatchStatus = status;
        AnimeRecord expected = before.Copy().Record; expected.WatchStatus = AnimeWatchStatusRules.GetStatus(requested);
        Assert.That(JsonSerializer.Serialize(saved.Record), Is.EqualTo(JsonSerializer.Serialize(expected)));
        Assert.That(saved.Revision, Is.EqualTo(before.Revision + (expected.WatchStatus == before.Record.WatchStatus ? 0 : 1)));
        Assert.That(vm.IsErrorOpen, Is.False);
    }

    [Test]
    public async Task CapturedMenuTarget_RemainsCorrectAfterFilteringAndNewCardInstances()
    {
        AnimeEntry first = await Add("A"), second = await Add("B");
        var vm = ViewModel(); await vm.ActivateAsync();
        AnimeCardTarget target = vm.Items.Single(card => card.Id == first.Id).Target;
        vm.Search = "B";
        Assert.That(vm.Items.Single().Id, Is.EqualTo(second.Id));
        await vm.ChangeStatusCommand.ExecuteAsync(new AnimeStatusChange(target, WatchStatus.Dropped));
        Assert.That((await _collection.GetByIdAsync(first.Id))!.Record.WatchStatus, Is.EqualTo(WatchStatus.Dropped));
        Assert.That((await _collection.GetByIdAsync(second.Id))!.Record.WatchStatus, Is.EqualTo(WatchStatus.Watching));
        Assert.That(vm.Items.Single().Id, Is.EqualTo(second.Id));
    }

    [Test]
    public async Task StaleStatusMenu_RefusesToOverwriteNewerPersonalRecord()
    {
        AnimeEntry entry = await Add();
        var vm = ViewModel(); await vm.ActivateAsync();
        AnimeCardTarget target = vm.Items.Single().Target;
        entry.Record.Notes = "Newer note"; entry = await _collection.UpdateRecordAsync(entry.Id, entry.Revision, entry.Record);
        entry = (await _collection.GetByIdAsync(entry.Id))!;
        await vm.ChangeStatusCommand.ExecuteAsync(new AnimeStatusChange(target, WatchStatus.Dropped));
        Assert.That(JsonSerializer.Serialize(await _collection.GetByIdAsync(entry.Id)), Is.EqualTo(JsonSerializer.Serialize(entry)));
        Assert.That(vm.IsErrorOpen, Is.True);
        Assert.That(vm.ErrorMessage, Does.StartWith("Bangumi_Conflict:"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task RemoveMenu_CancelOrConfirm_OnlyAffectsCapturedEntry_AndCanUndo(bool confirm)
    {
        AnimeEntry first = await Add("A"), second = await Add("B");
        _dialogs.Setup(dialogs => dialogs.ConfirmDeleteAsync(first.Metadata.Title)).ReturnsAsync(confirm);
        var vm = ViewModel(); await vm.ActivateAsync();
        AnimeCardTarget target = vm.Items.Single(card => card.Id == first.Id).Target;
        vm.Search = "B";
        await vm.RemoveAnimeCommand.ExecuteAsync(target);
        _dialogs.Verify(dialogs => dialogs.ConfirmDeleteAsync("A"), Times.Once);
        Assert.That(await _collection.GetByIdAsync(second.Id), Is.Not.Null);
        Assert.That(await _repository.GetTrashAsync(), Has.Count.EqualTo(confirm ? 1 : 0));
        Assert.That(vm.CanUndoDelete, Is.EqualTo(confirm));
        if (confirm) await vm.UndoDeleteCommand.ExecuteAsync(null);
        Assert.That(JsonSerializer.Serialize((await _collection.GetByIdAsync(first.Id))!.Record), Is.EqualTo(JsonSerializer.Serialize(first.Record)));
        Assert.That(await _repository.GetTrashAsync(), Is.Empty);
    }

    [Test]
    public async Task ChangeWhileDeleteConfirmationIsOpen_IsRejectedWithoutArchiving()
    {
        AnimeEntry entry = await Add();
        var answer = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _dialogs.Setup(dialogs => dialogs.ConfirmDeleteAsync(It.IsAny<string>())).Returns(answer.Task);
        var vm = ViewModel(); await vm.ActivateAsync();
        Task remove = vm.RemoveAnimeCommand.ExecuteAsync(vm.Items.Single().Target);
        entry.Record.Notes = "Changed during dialog";
        entry = await _collection.UpdateRecordAsync(entry.Id, entry.Revision, entry.Record);
        entry = (await _collection.GetByIdAsync(entry.Id))!;
        answer.SetResult(true); await remove;
        Assert.That(await _repository.GetTrashAsync(), Is.Empty);
        Assert.That(JsonSerializer.Serialize(await _collection.GetByIdAsync(entry.Id)), Is.EqualTo(JsonSerializer.Serialize(entry)));
        Assert.That(vm.IsErrorOpen, Is.True);
    }

    [Test]
    public async Task FailedStatusWrite_ShowsErrorAndKeepsPersistedCardState()
    {
        AnimeEntry entry = await Add();
        var failing = new Mock<IAnimeRepository>();
        failing.Setup(repo => repo.GetAllAsync()).Returns(() => _collection.GetAllAsync());
        failing.Setup(repo => repo.GetByIdAsync(entry.Id)).Returns(() => _collection.GetByIdAsync(entry.Id));
        failing.Setup(repo => repo.UpdateAsync(It.IsAny<AnimeEntry>())).ThrowsAsync(new IOException("Simulated write failure"));
        var vm = ViewModel(new(failing.Object, _covers, _log)); await vm.ActivateAsync();
        await vm.ChangeStatusCommand.ExecuteAsync(new AnimeStatusChange(vm.Items.Single().Target, WatchStatus.OnHold));
        Assert.That(vm.Items.Single().Target.Status, Is.EqualTo(WatchStatus.Watching));
        Assert.That(vm.IsErrorOpen, Is.True);
        Assert.That(JsonSerializer.Serialize(await _collection.GetByIdAsync(entry.Id)), Is.EqualTo(JsonSerializer.Serialize(entry)));
    }

    [TestCase(null, false)]
    [TestCase("328609", true)]
    public async Task DownloadInformation_OnlyNavigatesToExplicitLinkOrRefreshPreview(string? externalId, bool refresh)
    {
        AnimeEntry entry = await Add(bangumiId: externalId);
        var vm = ViewModel(); await vm.ActivateAsync();
        AnimeCardTarget target = vm.Items.Single().Target;
        vm.EditAnimeCommand.Execute(target);
        _navigation.Verify(nav => nav.NavigateTo(typeof(AnimeEditViewModel).FullName!, entry.Id, false, ""), Times.Once);
        vm.DownloadInfoCommand.Execute(target);
        _navigation.Verify(nav => nav.NavigateTo(typeof(AnimeLookupViewModel).FullName!,
            It.Is<AnimeLookupRequest>(request => request.ExistingId == entry.Id && request.Refresh == refresh), false, ""), Times.Once);
        Assert.That(JsonSerializer.Serialize(await _collection.GetByIdAsync(entry.Id)), Is.EqualTo(JsonSerializer.Serialize(entry)));
    }

    [Test]
    public async Task LibraryStartsWithAll_AndRetainsCombinedConditionsAcrossNavigation()
    {
        await Add("A"); await Add("B", WatchStatus.Planned);
        var vm = ViewModel(); await vm.ActivateAsync();
        Assert.That(vm.Items, Has.Count.EqualTo(2));
        vm.Search = "original A"; vm.StatusIndex = 2; vm.SelectedTag = "Music"; vm.SortIndex = 3; vm.SortDescending = false;
        vm.OnNavigatedFrom(); await vm.ActivateAsync();
        Assert.That(vm.Items.Single().Title, Is.EqualTo("A"));
        Assert.That(vm.HasFilters, Is.True);
        Assert.That(vm.SortDescending, Is.False);
        vm.ClearSearchCommand.Execute(null);
        Assert.That(vm.StatusIndex, Is.EqualTo(2)); Assert.That(vm.SelectedTag, Is.EqualTo("Music"));
        vm.ClearFiltersCommand.Execute(null);
        Assert.That(vm.Items, Has.Count.EqualTo(2)); Assert.That(vm.SortIndex, Is.EqualTo(3));
    }

    [Test]
    public void PrimaryRoutes_AreDistinctAndLandingPagesOnlyNavigateToLibrary()
    {
        var pages = new LemonPageService();
        Assert.That(pages.GetPageType(typeof(LemonHomeViewModel).FullName!), Is.EqualTo(typeof(LemonHomePage)));
        Assert.That(pages.GetPageType(typeof(AnimeLibraryViewModel).FullName!), Is.EqualTo(typeof(AnimeLibraryPage)));
        Assert.That(pages.GetPageType(typeof(LemonCategoriesViewModel).FullName!), Is.EqualTo(typeof(LemonCategoriesPage)));
        Assert.That(pages.GetPageType(typeof(LemonRecognitionViewModel).FullName!), Is.EqualTo(typeof(LemonRecognitionPage)));
        foreach (var vm in new LemonLandingViewModel[] { new LemonHomeViewModel(_navigation.Object, _collection, new(), _covers, _log, (key, _) => key),
                     new LemonCategoriesViewModel(_navigation.Object, _collection, new(), _covers, _log, (key, _) => key),
                     new LemonRecognitionViewModel(_navigation.Object, Mock.Of<IRecognitionImageLoader>(), Mock.Of<IImageRecognitionService>(),
                         Mock.Of<IRecognitionUiService>(), _collection, _log, (key, _) => key) })
            vm.OpenLibraryCommand.Execute(null);
        _navigation.Verify(nav => nav.NavigateTo(typeof(AnimeLibraryViewModel).FullName!, null, false, ""), Times.Exactly(3));
        Assert.That(pages.IsAllowed(typeof(GalgameManager.Views.HomePage)), Is.False);
    }

    [Test]
    public async Task TagSourceRebinding_CannotClearAStillValidFilter()
    {
        AnimeEntry entry = await Add();
        var vm = ViewModel(); await vm.ActivateAsync(); vm.SelectedTag = "Music";
        // Match the ComboBox's transient deselection when ItemsSource is replaced.
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(vm.AvailableTags)) vm.SelectedTag = null;
        };
        entry.Record.Tags.Add("New tag");
        await _collection.UpdateRecordAsync(entry.Id, entry.Revision, entry.Record);
        vm.OnNavigatedFrom(); await vm.ActivateAsync();
        Assert.That(vm.AvailableTags, Does.Contain("New tag"));
        Assert.That(vm.SelectedTag, Is.EqualTo("Music"));
        Assert.That(vm.Items.Single().Id, Is.EqualTo(entry.Id));
    }

    [Test]
    public void SharedCardLayout_PreservesCoverAndKeepsFinalContentNearBottom()
    {
        string root = FindRoot();
        XDocument card = XDocument.Load(Path.Combine(root, "GalgameManager/Views/Anime/AnimeCardControl.xaml"));
        XNamespace ns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XElement grid = card.Root!.Element(ns + "Grid")!;
        // The final visual request specifies physical pixels, rather than a fixed 4 DIP.
        Assert.That(grid.Attribute("Padding")!.Value.Split(',').Last(), Is.EqualTo("3"));
        // A collapsed unknown-total progress bar must not leave a trailing Grid row gap.
        Assert.That(grid.Attribute("RowSpacing")!.Value, Is.EqualTo("0"));
        Assert.That(grid.Element(ns + "Grid.RowDefinitions")!.Elements().First().Attribute("Height")!.Value, Is.EqualTo("244"));
        Assert.That(card.Root.Attribute("Height"), Is.Null);
        Assert.That(grid.Elements().Last().Name.LocalName, Is.EqualTo("ProgressBar"));
    }
    private static string FindRoot()
    {
        var folder = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "GalgameManager.sln"))) folder = folder.Parent;
        return folder?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
