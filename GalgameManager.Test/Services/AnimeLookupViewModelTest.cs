using GalgameManager.Contracts.Services;
using LemonVN.Application.Anime;
using LemonVN.Contracts.Services;
using LemonVN.Domain.Anime;
using LemonVN.Infrastructure.Anime;
using LemonVN.Infrastructure.Bangumi;
using LemonVN.Services;
using LemonVN.ViewModels.Anime;
using Moq;

namespace GalgameManager.Test.Services;

[TestFixture]
public sealed class AnimeLookupViewModelTest
{
    private AppDataPathService _paths = null!;
    private AppDatabaseService _database = null!;
    private AnimeCollectionService _collection = null!;
    private AnimeMetadataService _metadata = null!;
    private Mock<IBangumiAnimeProvider> _provider = null!;
    private Mock<IRemoteAnimeCoverCache> _covers = null!;
    private Mock<INavigationService> _navigation = null!;
    private AppLogService _log = null!;
    private readonly BangumiAnime _source = new("100", "Test animation", null, "Summary", new(2024, 1, 1), 12, null, null);
    private static string Text(string key, object[] args) => key + ":" + string.Join(",", args);

    [SetUp]
    public void SetUp()
    {
        _paths = new(Path.Combine(TestEnvironmentSetup.Root, "M5LookupUi", Guid.NewGuid().ToString("N")));
        _database = new(_paths); _database.Initialize();
        var covers = new CoverStorageService(_paths);
        _log = new(_paths);
        _collection = new(new LiteDbAnimeRepository(_database), covers, _log);
        _provider = new(); _covers = new(); _navigation = new();
        _provider.Setup(provider => provider.SearchAnimeAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BangumiSearchResults([_source], 1, 0));
        _provider.Setup(provider => provider.GetAnimeDetailsAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(_source);
        _covers.Setup(covers => covers.GetBytesAsync(It.IsAny<Uri?>(), It.IsAny<CancellationToken>(), It.IsAny<bool>())).ReturnsAsync((byte[]?)null);
        _covers.Setup(covers => covers.GetDisplaySourceAsync(It.IsAny<Uri?>(), It.IsAny<CancellationToken>())).ReturnsAsync(CoverStorageService.Placeholder);
        _metadata = new(_provider.Object, _covers.Object, _collection, covers);
    }
    [TearDown] public void TearDown() { _database.Dispose(); _log.Dispose(); }
    private AnimeLookupViewModel ViewModel() => new(_provider.Object, _metadata, _covers.Object, _collection, _navigation.Object, _log, Text);

    [Test]
    public async Task ExplicitSearchThenPreview_HasNoWriteUntilConfirmation_AndManualEntryRemainsAvailable()
    {
        var viewModel = ViewModel(); await viewModel.ActivateAsync(new());
        viewModel.Keyword = "中文标题";
        _provider.Verify(provider => provider.SearchAnimeAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        await viewModel.SearchCommand.ExecuteAsync(null);
        Assert.That(viewModel.Items, Has.Count.EqualTo(1));
        await viewModel.PreviewCommand.ExecuteAsync(viewModel.Items.Single());
        Assert.That(viewModel.HasPreview, Is.True);
        Assert.That(await _collection.GetAllAsync(), Is.Empty);
        await viewModel.ConfirmCommand.ExecuteAsync(null);
        AnimeEntry saved = (await _collection.GetAllAsync()).Single();
        Assert.That(saved.Metadata.BangumiId, Is.EqualTo("100"));
        _navigation.Verify(navigation => navigation.NavigateTo(typeof(AnimeDetailViewModel).FullName!, saved.Id, true, ""), Times.Once);
        viewModel.ManualCommand.Execute(null);
        _navigation.Verify(navigation => navigation.NavigateTo(typeof(AnimeEditViewModel).FullName!, null, false, ""), Times.Once);
    }

    [Test]
    public async Task DuplicatePreview_OffersOpeningExisting_WithoutAnotherSave()
    {
        AnimeEntry entry = await _metadata.CommitAsync(await _metadata.PrepareAsync("100", null, false, default), default);
        var viewModel = ViewModel(); await viewModel.ActivateAsync(new());
        await viewModel.PreviewCommand.ExecuteAsync(new BangumiResultItem(_source));
        Assert.That(viewModel.HasDuplicate, Is.True);
        Assert.That(viewModel.HasPreview, Is.False);
        viewModel.OpenExistingCommand.Execute(null);
        _navigation.Verify(navigation => navigation.NavigateTo(typeof(AnimeDetailViewModel).FullName!, entry.Id, true, ""), Times.Once);
        Assert.That(await _collection.GetAllAsync(), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task CancelAndLeavingPage_PreventLateSearchStateOrDatabaseWrites()
    {
        _provider.Setup(provider => provider.SearchAnimeAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, int _, CancellationToken token) =>
            {
                await Task.Delay(10_000, token);
                return new BangumiSearchResults([_source], 1, 0);
            });
        var viewModel = ViewModel(); await viewModel.ActivateAsync(new()); viewModel.Keyword = "test";
        Task search = viewModel.SearchCommand.ExecuteAsync(null);
        viewModel.CancelCommand.Execute(null);
        await search;
        Assert.That(viewModel.IsBusy, Is.False);
        Assert.That(viewModel.Message, Does.StartWith("Bangumi_Cancelled:"));
        Assert.That(viewModel.Items, Is.Empty);
        search = viewModel.SearchCommand.ExecuteAsync(null); viewModel.OnNavigatedFrom(); await search;
        Assert.That(await _collection.GetAllAsync(), Is.Empty);
    }

    [Test]
    public async Task RevisionConflictAfterPreview_InvalidatesConfirmation_AndPreservesLatestNote()
    {
        AnimeEntry entry = await _metadata.CommitAsync(await _metadata.PrepareAsync("100", null, false, default), default);
        var viewModel = ViewModel(); await viewModel.ActivateAsync(new(entry.Id, Refresh: true));
        Assert.That(viewModel.HasPreview, Is.True);
        entry.Record.Notes = "Latest note"; await _collection.SaveAsync(entry, false);
        await viewModel.ConfirmCommand.ExecuteAsync(null);
        Assert.That(viewModel.HasPreview, Is.False);
        Assert.That(viewModel.Message, Does.StartWith("Bangumi_Conflict:"));
        Assert.That((await _collection.GetByIdAsync(entry.Id))!.Record.Notes, Is.EqualTo("Latest note"));
        _navigation.Verify(navigation => navigation.NavigateTo(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<bool>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task AuthenticationFailure_IsDistinct_AndCannotDisableLocalCollectionOperations()
    {
        _provider.Setup(provider => provider.SearchAnimeAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BangumiRequestException(BangumiFailure.Authentication));
        var viewModel = ViewModel(); await viewModel.ActivateAsync(new()); viewModel.Keyword = "test";
        await viewModel.SearchCommand.ExecuteAsync(null);
        Assert.That(viewModel.Message, Does.StartWith("Bangumi_Error_Authentication:"));
        Assert.That(viewModel.CanInteract, Is.True);
        AnimeEntry manual = await _collection.SaveAsync(new() { Metadata = new() { Title = "Offline still works" } }, true);
        Assert.That(await _collection.GetByIdAsync(manual.Id), Is.Not.Null);
    }

    [Test]
    public void NetworkPreference_UsesCompatibleSettings_AndThemeSaveDoesNotResetIt()
    {
        _paths.EnsureDirectories();
        File.WriteAllText(Path.Combine(_paths.SettingsPath, "settings.json"), "{\"Application\":\"LemonVN.Desktop\",\"FormatVersion\":1,\"Theme\":0}");
        var settings = new AppSettingsService(_paths); settings.Initialize();
        Assert.That(settings.BangumiEnabled, Is.True);
        settings.SaveBangumiEnabled(false); settings.SaveTheme(ThemePreference.Dark);
        settings = new(_paths); settings.Initialize();
        Assert.That(settings.BangumiEnabled, Is.False);
        Assert.That(settings.Theme, Is.EqualTo(ThemePreference.Dark));
    }

    [Test]
    public async Task LateCoverAfterCancellation_CannotRestorePreviewOrOldNotice()
    {
        var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _covers.Setup(covers => covers.GetDisplaySourceAsync(It.IsAny<Uri?>(), It.IsAny<CancellationToken>()))
            .Returns((Uri? _, CancellationToken _) => { started.SetResult(); return pending.Task; });
        var viewModel = ViewModel(); await viewModel.ActivateAsync(new());
        viewModel.Notice = "Old warning"; viewModel.IsNoticeOpen = true;
        Task preview = viewModel.PreviewCommand.ExecuteAsync(new BangumiResultItem(_source));
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(viewModel.IsNoticeOpen, Is.False);
        viewModel.CancelCommand.Execute(null);
        pending.SetResult("C:/synthetic-late-image.jpg");
        await preview;
        Assert.That(viewModel.Cover, Is.EqualTo(CoverStorageService.Placeholder));
        Assert.That(viewModel.HasPreview, Is.False);
        Assert.That(viewModel.CanConfirm, Is.False);
        Assert.That(viewModel.Notice, Is.Empty);
        Assert.That(await _collection.GetAllAsync(), Is.Empty);
    }
}
