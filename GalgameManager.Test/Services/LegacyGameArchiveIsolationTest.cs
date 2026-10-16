// LemonVN modification from the fixed PotatoVN baseline; see LICENSE, NOTICE and UPSTREAM_PROVENANCE.json.
using GalgameManager.Models.BgTasks;

namespace GalgameManager.Test.Services;

[TestFixture]
public sealed class LegacyGameArchiveIsolationTest
{
    [TestCase(".7z")]
    [TestCase(".7Z")]
    [TestCase(".001")]
    public async Task NativeGameArchive_RunAndRecoveryRejectBeforeChangingFiles(string extension)
    {
        string root = Path.Combine(TestEnvironmentSetup.Root, "NativeArchiveIsolation", Guid.NewGuid().ToString("N"));
        string oldTemporary = Path.Combine(root, "existing-temporary");
        Directory.CreateDirectory(oldTemporary);
        string archive = Path.Combine(root, "synthetic" + extension);
        string sentinel = Path.Combine(oldTemporary, "keep.txt");
        await File.WriteAllTextAsync(archive, "Synthetic archive; must never be opened as an archive.");
        await File.WriteAllTextAsync(sentinel, "Preserve recovery data.");
        var task = new UnpackGameTask
        {
            PackPath = archive, GameName = "synthetic-game", TargetPath = root, TmpDirName = "existing-temporary"
        };

        Task execution = task.Run();
        Assert.ThrowsAsync<NotSupportedException>(async () => await execution);
        Assert.That(task.Task, Is.SameAs(execution).And.Property(nameof(Task.IsFaulted)).True);
        Assert.ThrowsAsync<NotSupportedException>(async () => await task.RecoverFromJson());
        Assert.Multiple(() =>
        {
            Assert.That(File.ReadAllText(sentinel), Is.EqualTo("Preserve recovery data."));
            Assert.That(File.ReadAllText(archive), Is.EqualTo("Synthetic archive; must never be opened as an archive."));
            Assert.That(Directory.GetDirectories(root), Is.EquivalentTo(new[] { oldTemporary }));
            Assert.That(Directory.GetFiles(root), Is.EquivalentTo(new[] { archive }));
        });
    }
}
