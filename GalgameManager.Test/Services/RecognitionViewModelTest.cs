using System.Text.Json;
using GalgameManager.Contracts.Services;
using LemonVN.Application.Anime;
using LemonVN.Contracts.Services;
using LemonVN.Domain.Anime;
using LemonVN.Infrastructure.Anime;
using LemonVN.Infrastructure.Recognition;
using LemonVN.Services;
using LemonVN.ViewModels;
using LemonVN.ViewModels.Anime;
using Moq;

namespace GalgameManager.Test.Services;

[TestFixture]
public sealed class RecognitionViewModelTest
{
    private AppDataPathService _paths = null!;
    private AppDatabaseService _db = null!;
    private AppLogService _log = null!;
    private AnimeCollectionService _collection = null!;
    private Mock<IRecognitionImageLoader> _loader = null!;
    private Mock<IImageRecognitionService> _recognition = null!;
    private Mock<IRecognitionUiService> _ui = null!;
    private Mock<INavigationService> _navigation = null!;
    private RecognitionImage _image = null!;
    private static readonly RecognitionCandidate Candidate = new("Known title", ["Known title", "Original title"],
        AniListId: 123, Episode: "7", FromSeconds: 12, ToSeconds: 14, Similarity: 0.9,
        Preview: new Uri("https://api.trace.moe/image/abcdefghijk"));
    private static string Text(string key, object[] args) => key + ":" + string.Join(",", args);

    [SetUp]
    public void SetUp()
    {
        _paths = new(Path.Combine(TestEnvironmentSetup.Root, "M55C", Guid.NewGuid().ToString("N")));
        _db = new(_paths); _db.Initialize(); _log = new(_paths); _log.Initialize();
        var covers = new CoverStorageService(_paths);
        _collection = new(new LiteDbAnimeRepository(_db), covers, _log);
        _image = RecognitionImageLoader.Normalize(RecognitionProviderTest.Image());
        _loader = new(); _recognition = new(); _ui = new(); _navigation = new();
        _ui.Setup(u => u.PickImageAsync()).ReturnsAsync("private-path-marker.png");
        _loader.Setup(l => l.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(_image);
        _ui.Setup(u => u.ConfirmUploadAsync(It.IsAny<RecognitionMode>())).ReturnsAsync(true);
        _recognition.Setup(r => r.RecognizeAsync(It.IsAny<RecognitionImage>(), It.IsAny<RecognitionMode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecognitionResult(RecognitionMode.Scene, [Candidate]));
        _recognition.Setup(r => r.GetPreviewAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>())).ReturnsAsync(_image.CopyBytes());
    }
    [TearDown] public void TearDown() { _db.Dispose(); _log.Dispose(); }
    private LemonRecognitionViewModel Model()
    {
        var model = new LemonRecognitionViewModel(_navigation.Object, _loader.Object, _recognition.Object, _ui.Object, _collection, _log, Text);
        model.OnNavigatedTo(null!); return model;
    }
    private async Task<AnimeEntry> Add(string title = "Known title") => await _collection.SaveAsync(new()
    {
        Metadata = new() { Title = title, OriginalTitle = "Original title", TotalEpisodes = 12, BangumiId = "999" },
        Record = new() { WatchStatus = WatchStatus.Planned, PersonalRating = 9, Notes = "personal-note", Tags = ["keep"],
            Episodes = [new() { EpisodeNumber = 2, IsWatched = true, Notes = "episode-note", WatchedAtUtc = DateTime.UtcNow }] }
    }, true);

    [Test]
    public async Task EntryPickAndModeChange_DoNotUploadOrReadRemoteData()
    {
        var model = Model();
        Assert.That(model.CanRecognize, Is.False);
        await model.RecognizeCommand.ExecuteAsync(null);
        await model.PickImageCommand.ExecuteAsync(null);
        Assert.That(model.InputPreview, Is.EqualTo(_image.CopyBytes()));
        Assert.That(model.CanRecognize, Is.True);
        model.ModeIndex = 1;
        _ui.Verify(u => u.ConfirmUploadAsync(It.IsAny<RecognitionMode>()), Times.Never);
        _recognition.Verify(r => r.RecognizeAsync(It.IsAny<RecognitionImage>(), It.IsAny<RecognitionMode>(), It.IsAny<CancellationToken>()), Times.Never);
        _recognition.Verify(r => r.GetPreviewAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task DeclinedConfirmation_DoesNotUpload()
    {
        _ui.Setup(u => u.ConfirmUploadAsync(It.IsAny<RecognitionMode>())).ReturnsAsync(false);
        var model = Model(); await model.PickImageCommand.ExecuteAsync(null);
        await model.RecognizeCommand.ExecuteAsync(null);
        _recognition.Verify(r => r.RecognizeAsync(It.IsAny<RecognitionImage>(), It.IsAny<RecognitionMode>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.That(model.IsBusy, Is.False); Assert.That(model.Items, Is.Empty);
    }

    [TestCase("cancel")]
    [TestCase("mode")]
    [TestCase("navigate")]
    public async Task StaleConfirmation_CannotUploadAfterCancellationOrNavigation(string action)
    {
        var confirmation = new TaskCompletionSource<bool>();
        _ui.Setup(u => u.ConfirmUploadAsync(It.IsAny<RecognitionMode>())).Returns(confirmation.Task);
        var model = Model(); await model.PickImageCommand.ExecuteAsync(null);
        Task pending = model.RecognizeCommand.ExecuteAsync(null);
        if (action == "cancel") model.CancelCommand.Execute(null);
        else if (action == "mode") model.ModeIndex = 1;
        else model.OnNavigatedFrom();
        confirmation.SetResult(true); await pending;
        _recognition.Verify(r => r.RecognizeAsync(It.IsAny<RecognitionImage>(), It.IsAny<RecognitionMode>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.That(model.Items, Is.Empty); Assert.That(model.IsBusy, Is.False);
    }

    [TestCase(RecognitionMode.Scene)]
    [TestCase(RecognitionMode.Character)]
    public async Task Confirmation_CapturesSelectedModeAndImage(RecognitionMode mode)
    {
        var model = Model(); await model.PickImageCommand.ExecuteAsync(null); model.ModeIndex = (int)mode;
        await model.RecognizeCommand.ExecuteAsync(null);
        _ui.Verify(u => u.ConfirmUploadAsync(mode), Times.Once);
        _recognition.Verify(r => r.RecognizeAsync(_image, mode, It.IsAny<CancellationToken>()), Times.Once);
        Assert.That(model.Items, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Cancellation_RejectsLateServiceResult_AndNavigationClearsImageAndSession()
    {
        var reply = new TaskCompletionSource<RecognitionResult>();
        _recognition.Setup(r => r.RecognizeAsync(It.IsAny<RecognitionImage>(), It.IsAny<RecognitionMode>(), It.IsAny<CancellationToken>())).Returns(reply.Task);
        var model = Model(); await model.PickImageCommand.ExecuteAsync(null);
        Task pending = model.RecognizeCommand.ExecuteAsync(null);
        model.CancelCommand.Execute(null);
        reply.SetResult(new(RecognitionMode.Scene, [Candidate])); await pending;
        Assert.That(model.Items, Is.Empty); Assert.That(model.InputPreview, Is.Not.Null);
        model.OnNavigatedFrom();
        Assert.That(model.InputPreview, Is.Null); Assert.That(model.MatchPreview, Is.Null);
        _recognition.Verify(r => r.ClearSession(), Times.Once);
    }

    [Test]
    public async Task LatePreview_DoesNotReappearAfterCancel()
    {
        var reply = new TaskCompletionSource<byte[]>();
        _recognition.Setup(r => r.GetPreviewAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>())).Returns(reply.Task);
        var model = Model(); await model.PickImageCommand.ExecuteAsync(null); await model.RecognizeCommand.ExecuteAsync(null);
        Task pending = model.PreviewCommand.ExecuteAsync(model.Items.Single());
        model.CancelCommand.Execute(null); reply.SetResult(_image.CopyBytes()); await pending;
        Assert.That(model.MatchPreview, Is.Null);
    }

    [Test]
    public async Task NameSelection_OnlyOffersLocalCandidatesAndNeverMapsAniListToBangumi()
    {
        AnimeEntry saved = await Add();
        string before = JsonSerializer.Serialize((await _collection.GetByIdAsync(saved.Id))!);
        var model = Model(); await model.PickImageCommand.ExecuteAsync(null); await model.RecognizeCommand.ExecuteAsync(null);
        _navigation.Verify(n => n.NavigateTo(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<bool>(), It.IsAny<string>()), Times.Never);
        await model.SelectCandidateCommand.ExecuteAsync(model.Items.Single());
        Assert.That(model.LocalMatches.Single().Id, Is.EqualTo(saved.Id));
        _navigation.Verify(n => n.NavigateTo(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<bool>(), It.IsAny<string>()), Times.Never);
        model.SearchBangumiCommand.Execute(null);
        _navigation.Verify(n => n.NavigateTo(typeof(AnimeLookupViewModel).FullName!,
            It.Is<AnimeLookupRequest>(request => request.Keyword == "Known title" && request.ExistingId == null && !request.Refresh), false, ""), Times.Once);
        Assert.That(JsonSerializer.Serialize(await _collection.GetByIdAsync(saved.Id)), Is.EqualTo(before));
        Assert.That(await _collection.GetAllAsync(), Has.Count.EqualTo(1));
        await model.OpenLocalCommand.ExecuteAsync(model.LocalMatches.Single());
        _navigation.Verify(n => n.NavigateTo(typeof(AnimeDetailViewModel).FullName!, saved.Id, false, ""), Times.Once);
    }

    [Test]
    public async Task MissingTitle_DoesNotSearchUsingAnAniListNumber()
    {
        _recognition.Setup(r => r.RecognizeAsync(It.IsAny<RecognitionImage>(), It.IsAny<RecognitionMode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecognitionResult(RecognitionMode.Scene, [Candidate with { Work = "", Aliases = [] }]));
        var model = Model(); await model.PickImageCommand.ExecuteAsync(null); await model.RecognizeCommand.ExecuteAsync(null);
        await model.SelectCandidateCommand.ExecuteAsync(model.Items.Single()); model.SearchBangumiCommand.Execute(null);
        Assert.That(model.SearchKeyword, Is.Empty);
        _navigation.Verify(n => n.NavigateTo(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<bool>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task NetworkFailure_DoesNotMutateCollectionOrExposeSensitiveErrorsInLogs()
    {
        AnimeEntry saved = await Add(); string before = JsonSerializer.Serialize(await _collection.GetByIdAsync(saved.Id));
        _recognition.Setup(r => r.RecognizeAsync(It.IsAny<RecognitionImage>(), It.IsAny<RecognitionMode>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("private-path-marker Authorization fake-sensitive-marker"));
        var model = Model(); await model.PickImageCommand.ExecuteAsync(null); await model.RecognizeCommand.ExecuteAsync(null);
        Assert.That(model.IsMessageOpen, Is.True); Assert.That(model.Items, Is.Empty);
        Assert.That(JsonSerializer.Serialize(await _collection.GetByIdAsync(saved.Id)), Is.EqualTo(before));
        _log.Dispose(); // Flush and release the writer before inspecting the actual persisted log.
        string logs = string.Join("", Directory.GetFiles(_paths.LogsPath).Select(File.ReadAllText));
        Assert.That(logs, Does.Contain("Recognition did not complete"));
        Assert.That(logs, Does.Not.Contain("private-path-marker").And.Not.Contain("fake-sensitive-marker").And.Not.Contain("Authorization"));
        Assert.That(model.Message, Does.Not.Contain("private-path-marker"));
    }

    [Test]
    public async Task BadReplacementImage_ClearsTheOldImageBeforeAnyUpload()
    {
        var model = Model(); await model.PickImageCommand.ExecuteAsync(null);
        _loader.Setup(l => l.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("private-path-marker"));
        await model.PickImageCommand.ExecuteAsync(null); await model.RecognizeCommand.ExecuteAsync(null);
        Assert.That(model.CanRecognize, Is.False); Assert.That(model.InputPreview, Is.Null); Assert.That(model.IsMessageOpen, Is.True);
        _recognition.Verify(r => r.RecognizeAsync(It.IsAny<RecognitionImage>(), It.IsAny<RecognitionMode>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task EmptyResult_IsExplicitAndDoesNotCreateEntries()
    {
        _recognition.Setup(r => r.RecognizeAsync(It.IsAny<RecognitionImage>(), It.IsAny<RecognitionMode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecognitionResult(RecognitionMode.Character, []));
        var model = Model(); await model.PickImageCommand.ExecuteAsync(null); await model.RecognizeCommand.ExecuteAsync(null);
        Assert.That(model.HasNoMatches, Is.True); Assert.That(model.Items, Is.Empty);
        Assert.That(await _collection.GetAllAsync(), Is.Empty);
    }

    [TestCase(true, "Recognition_QuotaBefore:")]
    [TestCase(false, "Recognition_Quota:")]
    public async Task QuotaPresentation_DistinguishesPreRequestFromSearchResponse(bool before, string expectedKey)
    {
        _recognition.Setup(r => r.RecognizeAsync(It.IsAny<RecognitionImage>(), It.IsAny<RecognitionMode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecognitionResult(RecognitionMode.Scene, [Candidate], new(100, 5, 1), QuotaBeforeRequest: before));
        var model = Model(); await model.PickImageCommand.ExecuteAsync(null); await model.RecognizeCommand.ExecuteAsync(null);
        Assert.That(model.QuotaText, Is.EqualTo(expectedKey + "95,100,1"));
    }

    [Test]
    public void LongTimestamps_DoNotWrapAfterTwentyFourHours()
    {
        var item = new RecognitionCandidateItem(Candidate with { FromSeconds = 90061.25 }, Text);
        Assert.That(item.Details, Does.Contain("25:01:01.250"));
    }
}
