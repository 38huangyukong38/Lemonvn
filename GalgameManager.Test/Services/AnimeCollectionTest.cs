using LemonVN.Application.Anime;
using LemonVN.Contracts.Services;
using LemonVN.Domain.Anime;
using LemonVN.Infrastructure.Anime;
using LemonVN.Services;
using Moq;

namespace GalgameManager.Test.Services;

[TestFixture]
public sealed class AnimeCollectionTest
{
    private AppDataPathService _paths = null!;
    private AppDatabaseService _database = null!;
    private CoverStorageService _covers = null!;
    private IAnimeRepository _repository = null!;
    private AnimeCollectionService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _paths = new(Path.Combine(TestEnvironmentSetup.Root, "Covers", Guid.NewGuid().ToString("N")));
        _database = new(_paths);
        _database.Initialize();
        _repository = new LiteDbAnimeRepository(_database);
        _covers = new(_paths);
        _service = new(_repository, _covers, new AppLogService(_paths));
    }

    [TearDown] public void TearDown() => _database.Dispose();

    private static string CoverFixture()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var file = Path.Combine(directory.FullName, "GalgameManager", "Assets", "LemonPlaceholder.png");
            if (File.Exists(file)) return file;
        }
        throw new FileNotFoundException("Missing test cover.");
    }

    [Test]
    public async Task CoverIsCopied_OriginalIsUntouched_AndDeleteIsRecoverable()
    {
        string source = CoverFixture();
        byte[] original = File.ReadAllBytes(source);
        AnimeEntry entry = await _service.SaveAsync(new() { Metadata = new() { Title = "Cover" } }, true, source);
        string ownedFile = Path.Combine(_paths.RootPath, entry.Metadata.CoverRef!);
        Assert.That(File.ReadAllBytes(ownedFile), Is.EqualTo(original));
        await _service.DeleteAsync(entry);
        Assert.That(File.Exists(ownedFile), Is.True);
        Assert.That(File.ReadAllBytes(source), Is.EqualTo(original));
        await _service.RestoreLastDeletedAsync();
        Assert.That((await _service.GetByIdAsync(entry.Id))!.Metadata.CoverRef, Is.EqualTo(entry.Metadata.CoverRef));
    }

    [Test]
    public async Task FailedCoverCopy_LeavesExistingEntryAndRecordIntact()
    {
        AnimeEntry entry = await _service.SaveAsync(new() { Metadata = new() { Title = "Original" } }, true, CoverFixture());
        string? previous = entry.Metadata.CoverRef;
        entry.Metadata.Title = "Should not persist";
        Assert.ThrowsAsync<FileNotFoundException>(() => _service.SaveAsync(entry, false, Path.Combine(_paths.RootPath, "missing.png")));
        AnimeEntry loaded = (await _service.GetByIdAsync(entry.Id))!;
        Assert.That(loaded.Metadata.Title, Is.EqualTo("Original"));
        Assert.That(loaded.Metadata.CoverRef, Is.EqualTo(previous));
    }

    [Test]
    public async Task FailedDatabaseSave_RemovesOnlyItsNewStagedCover()
    {
        _paths.EnsureDirectories();
        var failingRepository = new Mock<IAnimeRepository>();
        failingRepository.Setup(repository => repository.AddAsync(It.IsAny<AnimeEntry>())).ThrowsAsync(new IOException("simulated disk failure"));
        var service = new AnimeCollectionService(failingRepository.Object, _covers, new AppLogService(_paths));
        Assert.ThrowsAsync<IOException>(() => service.SaveAsync(new() { Metadata = new() { Title = "Failure" } }, true, CoverFixture()));
        Assert.That(Directory.GetFiles(_paths.CoversPath), Is.Empty);
        Assert.That(await _service.GetAllAsync(), Is.Empty);
    }

    [Test]
    public async Task RecordOnlyUpdate_DoesNotTouchMetadataOrCover()
    {
        AnimeEntry entry = await _service.SaveAsync(new() { Metadata = new() { Title = "Metadata" } }, true, CoverFixture());
        AnimeEntry updated = await _service.UpdateRecordAsync(entry.Id, entry.Revision,
            new() { WatchStatus = WatchStatus.Watching, PersonalRating = 8, Notes = "my record", Episodes = [new() { EpisodeNumber = 4, IsWatched = true }] });
        Assert.That(updated.Metadata.Title, Is.EqualTo("Metadata"));
        Assert.That(updated.Metadata.CoverRef, Is.EqualTo(entry.Metadata.CoverRef));
        Assert.That(updated.Record.WatchedCount, Is.EqualTo(1));
        Assert.That(updated.Record.PersonalRating, Is.EqualTo(8));
    }

    [TestCase("../outside.png")] [TestCase("C:/outside.png")] [TestCase("Covers/../../outside.png")]
    public void UnsafeCoverReference_CannotReadOrDeleteExternalFiles(string reference)
    {
        Assert.That(_covers.GetDisplaySource(reference), Is.EqualTo(CoverStorageService.Placeholder));
        Assert.Throws<InvalidDataException>(() => _covers.RemoveUncommitted(reference));
    }

    [Test]
    public void MalformedCover_IsRejectedBeforeDatabaseWrite()
    {
        string badImage = Path.Combine(_paths.RootPath, "bad.png");
        File.WriteAllText(badImage, "not an image");
        Assert.ThrowsAsync<InvalidDataException>(() => _service.SaveAsync(new() { Metadata = new() { Title = "Bad cover" } }, true, badImage));
    }

    [Test]
    public async Task MissingOwnedCover_FallsBackWithoutDroppingTheEntry()
    {
        AnimeEntry entry = await _service.SaveAsync(new() { Metadata = new() { Title = "No image" } }, true, CoverFixture());
        File.Delete(Path.Combine(_paths.RootPath, entry.Metadata.CoverRef!));
        Assert.That(_covers.GetDisplaySource(entry.Metadata.CoverRef), Is.EqualTo(CoverStorageService.Placeholder));
        Assert.That(await _service.GetByIdAsync(entry.Id), Is.Not.Null);
    }
}
