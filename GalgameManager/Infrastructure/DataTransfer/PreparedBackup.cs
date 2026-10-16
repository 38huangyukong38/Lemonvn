using LemonVN.Domain.Anime;
using LemonVN.Services;

namespace LemonVN.Infrastructure.Backup;

public sealed class PreparedBackup : IDisposable
{
    internal AnimeLibrarySnapshot Snapshot { get; }
    internal BackupManifest Manifest { get; }
    internal string StagePath { get; }
    internal string ExpectedSignature { get; }
    internal string TempRoot { get; }
    private readonly AppLogService _log;
    internal bool IsDisposed { get; private set; }
    public int AnimeCount => Snapshot.Entries.Count;
    public int TrashCount => Snapshot.Trash.Count;
    public int CurrentAnimeCount { get; }
    public int CurrentTrashCount { get; }
    public DateTime ExportedAtUtc => Manifest.ExportedAtUtc;

    internal PreparedBackup(AnimeLibrarySnapshot snapshot, BackupManifest manifest, string stagePath, string tempRoot,
        AnimeLibrarySnapshot current, AppLogService log)
    {
        Snapshot = snapshot;
        Manifest = manifest;
        StagePath = stagePath;
        TempRoot = tempRoot;
        ExpectedSignature = current.Signature;
        CurrentAnimeCount = current.Entries.Count;
        CurrentTrashCount = current.Trash.Count;
        _log = log;
    }

    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        try
        {
            string target = Path.GetFullPath(StagePath);
            string allowed = Path.GetFullPath(TempRoot) + Path.DirectorySeparatorChar;
            string name = Path.GetFileName(target);
            if (!target.StartsWith(allowed, StringComparison.OrdinalIgnoreCase) || !name.StartsWith("LemonVN-Import-", StringComparison.Ordinal) ||
                !Guid.TryParseExact(name["LemonVN-Import-".Length..], "N", out _))
                throw new IOException("Refusing to remove an unowned staging directory.");
            if (new DirectoryInfo(TempRoot).Attributes.HasFlag(FileAttributes.ReparsePoint))
                throw new IOException("Refusing cleanup through a linked temp directory.");
            if (Directory.Exists(target) && !new DirectoryInfo(target).Attributes.HasFlag(FileAttributes.ReparsePoint))
                Directory.Delete(target, recursive: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            _log.Error(error, "Owned import staging data retained after cleanup failure.");
        }
    }
}
