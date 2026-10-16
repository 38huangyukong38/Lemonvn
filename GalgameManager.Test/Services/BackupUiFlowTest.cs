using GalgameManager.Contracts.Services;
using LemonVN.Application.Anime;
using LemonVN.Contracts.Services;
using LemonVN.Domain.Anime;
using LemonVN.Infrastructure.Anime;
using LemonVN.Infrastructure.Backup;
using LemonVN.Services;
using LemonVN.ViewModels;
using LemonVN.ViewModels.Anime;
using Moq;

namespace GalgameManager.Test.Services;

[TestFixture]
public sealed class BackupUiFlowTest
{
    private AppDataPathService _paths = null!;
    private AppDatabaseService _database = null!;
    private LiteDbAnimeRepository _repository = null!;
    private AnimeCollectionService _collection = null!;
    private AnimeBackupService _backup = null!;
    private AppLogService _log = null!;
    private Mock<IBackupUiService> _dialogs = null!;
    private Mock<INavigationService> _navigation = null!;
    private static string Text(string key, object[] args) => key + ":" + string.Join(",", args);

    [SetUp]
    public void SetUp()
    {
        _paths = new(Path.Combine(TestEnvironmentSetup.Root, "M4Ui", Guid.NewGuid().ToString("N")));
        _database = new(_paths);
        _database.Initialize();
        _repository = new(_database);
        _log = new(_paths);
        var covers = new CoverStorageService(_paths);
        _collection = new(_repository, covers, _log);
        _backup = new(_repository, _collection, covers, _paths, _log);
        _dialogs = new();
        _navigation = new();
    }
    [TearDown] public void TearDown() => _database.Dispose();
    private Task<AnimeEntry> Add(string title) => _collection.SaveAsync(new() { Metadata = new() { Title = title } }, true);

    [Test]
    public async Task RestoreCancel_AndInvalidZip_NeverReplaceOrNavigate()
    {
        AnimeEntry old = await Add("Keep");
        string zip = Path.Combine(_paths.RootPath, "backup.zip");
        await _backup.ExportAsync(zip);
        _dialogs.Setup(dialog => dialog.PickRestoreAsync()).ReturnsAsync(zip);
        _dialogs.Setup(dialog => dialog.ConfirmRestoreAsync(It.IsAny<PreparedBackup>())).ReturnsAsync(false);
        var viewModel = new BackupSettingsViewModel(_backup, _dialogs.Object, _navigation.Object, _log, Text);
        await viewModel.RestoreCommand.ExecuteAsync(null);
        Assert.That(viewModel.Message, Is.EqualTo("Backup_Cancelled:"));
        Assert.That((await _repository.GetByIdAsync(old.Id))!.Revision, Is.EqualTo(old.Revision));
        _navigation.Verify(service => service.NavigateTo(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<bool>(), It.IsAny<string>()), Times.Never);
        _dialogs.Invocations.Clear();
        File.WriteAllText(zip, "not a ZIP");
        await viewModel.RestoreCommand.ExecuteAsync(null);
        Assert.That(viewModel.Message, Does.StartWith("Backup_Failed:"));
        _dialogs.Verify(dialog => dialog.ConfirmRestoreAsync(It.IsAny<PreparedBackup>()), Times.Never);
        Assert.That(await _collection.GetAllAsync(), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task SuccessfulRestore_RefreshesNavigationWithCommittedResult()
    {
        await Add("Saved");
        string zip = Path.Combine(_paths.RootPath, "backup.zip");
        await _backup.ExportAsync(zip);
        _dialogs.Setup(dialog => dialog.PickRestoreAsync()).ReturnsAsync(zip);
        _dialogs.Setup(dialog => dialog.ConfirmRestoreAsync(It.IsAny<PreparedBackup>())).ReturnsAsync(true);
        var viewModel = new BackupSettingsViewModel(_backup, _dialogs.Object, _navigation.Object, _log, Text);
        await viewModel.RestoreCommand.ExecuteAsync(null);
        Assert.That(viewModel.IsBusy, Is.False);
        Assert.That(viewModel.Message, Does.StartWith("Backup_Restored:"));
        _navigation.Verify(service => service.NavigateTo(typeof(AnimeLibraryViewModel).FullName!,
            It.Is<object>(value => value is BackupRestoreResult), true, ""), Times.Once);
        Assert.That(_database.IsOpen, Is.True);
    }

    [Test]
    public async Task Purge_RequiresBothConfirmations_AndCancelPreservesArchivedEntry()
    {
        AnimeEntry entry = await Add("Archived");
        await _collection.DeleteAsync(entry);
        var viewModel = new AnimeRecycleViewModel(_repository, _collection, _backup, _dialogs.Object, _log, Text);
        var item = new AnimeTrashItem(entry, viewModel.RestoreCommand, viewModel.PurgeCommand);
        _dialogs.Setup(dialog => dialog.ConfirmPurgeAsync(entry.Metadata.Title, false)).ReturnsAsync(true);
        _dialogs.Setup(dialog => dialog.ConfirmPurgeAsync(entry.Metadata.Title, true)).ReturnsAsync(false);
        await viewModel.PurgeCommand.ExecuteAsync(item);
        Assert.That(await _repository.GetTrashAsync(), Has.Count.EqualTo(1));
        _dialogs.Setup(dialog => dialog.ConfirmPurgeAsync(entry.Metadata.Title, true)).ReturnsAsync(true);
        await viewModel.PurgeCommand.ExecuteAsync(item);
        Assert.That(await _repository.GetTrashAsync(), Is.Empty);
        Assert.That(viewModel.Message, Does.StartWith("Recycle_Purged:"));
        Assert.That(Directory.GetFiles(_paths.BackupsPath, "*.zip"), Has.Length.EqualTo(1));
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task RecycleRestore_ClearsOnlyMatchingUndo_AndKeepsOtherDeletionRecoverable(bool matchingUndo)
    {
        AnimeEntry entry = await Add("Restore this entry");
        await _collection.DeleteAsync(entry);
        AnimeEntry? other = null;
        if (!matchingUndo)
        {
            other = await Add("Keep the latest undo");
            await _collection.DeleteAsync(other);
        }
        var viewModel = new AnimeRecycleViewModel(_repository, _collection, _backup, _dialogs.Object, _log, Text);
        var item = new AnimeTrashItem(entry, viewModel.RestoreCommand, viewModel.PurgeCommand);
        await viewModel.RestoreCommand.ExecuteAsync(item);
        Assert.That((await _repository.GetByIdAsync(entry.Id))!.Revision, Is.GreaterThan(entry.Revision));
        Assert.That(_collection.LastDeletedId, Is.EqualTo(other?.Id));
        Assert.That(viewModel.Message, Does.StartWith("Recycle_Restored:"));
        if (other is not null)
        {
            await _collection.RestoreLastDeletedAsync();
            Assert.That(await _repository.GetByIdAsync(other.Id), Is.Not.Null);
        }
        Assert.That(await _repository.GetTrashAsync(), Is.Empty);
    }
}
