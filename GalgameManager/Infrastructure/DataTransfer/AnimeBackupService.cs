using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using LemonVN.Application.Anime;
using LemonVN.Contracts.Services;
using LemonVN.Domain.Anime;
using LemonVN.Infrastructure.Anime;
using LemonVN.Services;

namespace LemonVN.Infrastructure.Backup;

public sealed class AnimeBackupService(IAnimeRepository repository, AnimeCollectionService collection,
    ICoverStorageService covers, IAppDataPathService paths, AppLogService log)
{
    private static readonly Regex CoverName = new("^covers/[0-9a-f]{32}_[0-9a-f]{32}\\.(png|jpg)$", RegexOptions.CultureInvariant);
    private static string ArchiveName(string reference) => "covers/" + reference["Covers/".Length..];
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    public async Task ExportAsync(string destination, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            AnimeLibrarySnapshot snapshot = await repository.GetSnapshotAsync(); // One DB read gate covers active + trash.
            await ExportSnapshotAsync(snapshot, destination, cancellationToken);
        }
        catch (UnauthorizedAccessException error)
        {
            // Windows replacement of a locked destination can report access denied.
            // Surface a consistent persistence failure while retaining the native cause.
            throw new IOException("Cannot write the backup destination; the previous file was preserved.", error);
        }
    }

    private async Task ExportSnapshotAsync(AnimeLibrarySnapshot snapshot, string destination, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        snapshot.Validate();
        if (!Path.IsPathFullyQualified(destination) || !string.Equals(Path.GetExtension(destination), ".zip", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Choose an absolute ZIP backup filename.");
        if (snapshot.Entries.Count + snapshot.Trash.Count > BackupFormat.MaxRecords)
            throw new InvalidDataException("Backup exceeds the supported record count.");
        byte[] anime = JsonSerializer.SerializeToUtf8Bytes(snapshot.Entries, BackupFormat.JsonOptions);
        byte[] trash = JsonSerializer.SerializeToUtf8Bytes(snapshot.Trash, BackupFormat.JsonOptions);
        if (anime.Length > BackupFormat.MaxJsonBytes || trash.Length > BackupFormat.MaxJsonBytes)
            throw new InvalidDataException("Backup JSON exceeds the size limit.");
        // Validate our serialized representation too; never export a backup we cannot restore.
        BackupFormat.ReadEntries(anime);
        BackupFormat.ReadEntries(trash);
        var manifest = new BackupManifest
        {
            AppVersion = typeof(AnimeEntry).Assembly.GetName().Version!.ToString(), ExportedAtUtc = DateTime.UtcNow,
            AnimeCount = snapshot.Entries.Count, TrashCount = snapshot.Trash.Count,
            AnimeSha256 = Hash(anime), TrashSha256 = Hash(trash)
        };
        string temporary = Path.GetFullPath(destination) + "." + Guid.NewGuid().ToString("N") + ".tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(temporary)!);
        bool created = false;
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                created = true;
                using (var zip = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: true))
                {
                    long total = anime.Length + trash.Length;
                    await WriteEntryAsync(zip, "anime.json", anime, token);
                    await WriteEntryAsync(zip, "trash.json", trash, token);
                    foreach (string reference in snapshot.Entries.Concat(snapshot.Trash).Select(entry => entry.Metadata.CoverRef)
                                 .OfType<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
                    {
                        token.ThrowIfCancellationRequested();
                        byte[] bytes = await covers.ReadOwnedAsync(reference, token);
                        total += bytes.Length;
                        if (total > BackupFormat.MaxTotalBytes) throw new InvalidDataException("Backup exceeds 512 MB uncompressed.");
                        string name = ArchiveName(reference);
                        manifest.Covers.Add(new(name, bytes.Length, Hash(bytes)));
                        await WriteEntryAsync(zip, name, bytes, token);
                    }
                    byte[] manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest, BackupFormat.JsonOptions);
                    if (manifestBytes.Length > BackupFormat.MaxManifestBytes || total + manifestBytes.Length > BackupFormat.MaxTotalBytes)
                        throw new InvalidDataException("Backup manifest or total size exceeds the supported limit.");
                    await WriteEntryAsync(zip, "manifest.json", manifestBytes, token);
                }
                file.Flush(flushToDisk: true);
            }
            token.ThrowIfCancellationRequested();
            File.Move(temporary, destination, overwrite: true); // Complete ZIP replaces destination only at this point.
        }
        finally
        {
            if (created && File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static async Task WriteEntryAsync(ZipArchive archive, string name, byte[] bytes, CancellationToken token)
    {
        using Stream stream = archive.CreateEntry(name, CompressionLevel.Optimal).Open();
        await stream.WriteAsync(bytes, token);
    }

    private static async Task<byte[]> ReadEntryAsync(ZipArchiveEntry entry, int limit, CancellationToken token)
    {
        if (entry.Length < 0 || entry.Length > limit) throw new InvalidDataException("ZIP entry exceeds its size limit.");
        using Stream input = entry.Open();
        using var output = new MemoryStream();
        byte[] buffer = new byte[64 * 1024];
        int count;
        while ((count = await input.ReadAsync(buffer, token)) != 0)
        {
            if (output.Length + count > limit) throw new InvalidDataException("Expanded ZIP entry exceeds its size limit.");
            await output.WriteAsync(buffer.AsMemory(0, count), token);
        }
        if (output.Length != entry.Length) throw new InvalidDataException("ZIP length mismatch.");
        return output.ToArray();
    }

    public async Task<PreparedBackup> PrepareAsync(string source, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        using var file = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length > BackupFormat.MaxTotalBytes) throw new InvalidDataException("Compressed backup exceeds 512 MB.");
        using var zip = new ZipArchive(file, ZipArchiveMode.Read);
        var items = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        if (zip.Entries.Count > BackupFormat.MaxRecords + 4) throw new InvalidDataException("Too many ZIP entries.");
        foreach (ZipArchiveEntry entry in zip.Entries)
        {
            if (entry.FullName == "covers/" && entry.Length == 0) continue;
            if (entry.FullName is not ("manifest.json" or "anime.json" or "trash.json") && !CoverName.IsMatch(entry.FullName))
                throw new InvalidDataException("Unexpected or unsafe ZIP path.");
            if (!items.TryAdd(entry.FullName, entry)) throw new InvalidDataException("Duplicate ZIP path.");
            total = checked(total + entry.Length);
            if (total > BackupFormat.MaxTotalBytes) throw new InvalidDataException("Expanded backup exceeds 512 MB.");
        }
        if (!items.TryGetValue("manifest.json", out ZipArchiveEntry? manifestEntry) ||
            !items.TryGetValue("anime.json", out ZipArchiveEntry? animeEntry) || !items.TryGetValue("trash.json", out ZipArchiveEntry? trashEntry))
            throw new InvalidDataException("Missing backup files.");
        byte[] manifestBytes = await ReadEntryAsync(manifestEntry, BackupFormat.MaxManifestBytes, token);
        using (JsonDocument document = JsonDocument.Parse(manifestBytes)) BackupFormat.CheckManifest(document.RootElement);
        BackupManifest manifest = JsonSerializer.Deserialize<BackupManifest>(manifestBytes, BackupFormat.JsonOptions)
            ?? throw new InvalidDataException("Missing manifest.");
        if (manifest.ExportFormatVersion is not (1 or BackupFormat.CurrentVersion) || manifest.Application != "LemonVN.Desktop" ||
            !Version.TryParse(manifest.AppVersion, out _) || manifest.ExportedAtUtc == default || manifest.ExportedAtUtc.Kind != DateTimeKind.Utc ||
            manifest.Covers is null || manifest.AnimeCount < 0 || manifest.TrashCount < 0 ||
            (long)manifest.AnimeCount + manifest.TrashCount > BackupFormat.MaxRecords)
            throw new InvalidDataException("Unsupported backup identity, format or counts.");
        byte[] anime = await ReadEntryAsync(animeEntry, BackupFormat.MaxJsonBytes, token);
        byte[] trash = await ReadEntryAsync(trashEntry, BackupFormat.MaxJsonBytes, token);
        if (Hash(anime) != manifest.AnimeSha256 || Hash(trash) != manifest.TrashSha256)
            throw new InvalidDataException("Backup JSON checksum mismatch.");
        var snapshot = new AnimeLibrarySnapshot(BackupFormat.ReadEntries(anime, manifest.ExportFormatVersion),
            BackupFormat.ReadEntries(trash, manifest.ExportFormatVersion));
        snapshot.Validate();
        if (snapshot.Entries.Count != manifest.AnimeCount || snapshot.Trash.Count != manifest.TrashCount)
            throw new InvalidDataException("Backup counts do not match manifest.");
        string[] expectedCovers = snapshot.Entries.Concat(snapshot.Trash).Select(entry => entry.Metadata.CoverRef)
            .OfType<string>().Select(ArchiveName).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (manifest.Covers.Any(cover => cover is null || string.IsNullOrEmpty(cover.Path) || !CoverName.IsMatch(cover.Path) || cover.Length <= 0 || cover.Length > CoverStorageService.MaxBytes) ||
            manifest.Covers.Select(cover => cover.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != manifest.Covers.Count ||
            !expectedCovers.SequenceEqual(manifest.Covers.Select(cover => cover.Path).Order(StringComparer.Ordinal)) ||
            items.Count != expectedCovers.Length + 3)
            throw new InvalidDataException("Cover references/resources do not match the backup.");
        Directory.CreateDirectory(paths.TempPath);
        if (new DirectoryInfo(paths.TempPath).Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new IOException("Import temp path must not be a link.");
        string stage = Path.Combine(paths.TempPath, "LemonVN-Import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        PreparedBackup? prepared = null;
        try
        {
            foreach (BackupCover cover in manifest.Covers)
            {
                if (!items.TryGetValue(cover.Path, out ZipArchiveEntry? entry)) throw new InvalidDataException("Missing cover asset.");
                byte[] bytes = await ReadEntryAsync(entry, CoverStorageService.MaxBytes, token);
                if (bytes.Length != cover.Length || Hash(bytes) != cover.Sha256) throw new InvalidDataException("Cover checksum mismatch.");
                string extension = CoverStorageService.ValidateBytes(bytes);
                if (Path.GetExtension(cover.Path) != extension) throw new InvalidDataException("Cover extension does not match image bytes.");
                // Only generated whitelisted basenames; never ExtractToDirectory or raw ZIP paths.
                await File.WriteAllBytesAsync(Path.Combine(stage, Path.GetFileName(cover.Path)), bytes, token);
            }
            AnimeLibrarySnapshot current = await repository.GetSnapshotAsync();
            prepared = new(snapshot, manifest, stage, paths.TempPath, current, log);
            return prepared;
        }
        catch
        {
            new PreparedBackup(snapshot, manifest, stage, paths.TempPath, new([], []), log).Dispose();
            throw;
        }
    }

    public async Task<BackupRestoreResult> RestoreAsync(PreparedBackup prepared, CancellationToken token = default) =>
        await collection.WithExclusiveWriteAsync(async () =>
        {
            if (prepared.IsDisposed) throw new ObjectDisposedException(nameof(prepared));
            token.ThrowIfCancellationRequested();
            if (new DirectoryInfo(prepared.StagePath).Attributes.HasFlag(FileAttributes.ReparsePoint) ||
                new DirectoryInfo(prepared.TempRoot).Attributes.HasFlag(FileAttributes.ReparsePoint))
                throw new IOException("Import staging directory must not be a filesystem link.");
            AnimeLibrarySnapshot current = await repository.GetSnapshotAsync();
            if (current.Signature != prepared.ExpectedSignature) throw new AnimeConflictException("Current library changed after preview; confirm again.");
            Directory.CreateDirectory(paths.BackupsPath);
            if (new DirectoryInfo(paths.BackupsPath).Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new IOException("Backups path must not be a link.");
            string recovery = Path.Combine(paths.BackupsPath, $"Before-Restore-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.zip");
            await ExportSnapshotAsync(current, recovery, token); // Durable recovery copy before any mutation.
            var incoming = new AnimeLibrarySnapshot(prepared.Snapshot.Entries.Select(entry => entry.Copy()).ToList(),
                prepared.Snapshot.Trash.Select(entry => entry.Copy()).ToList());
            var staged = new List<string>();
            try
            {
                foreach (AnimeEntry entry in incoming.Entries.Concat(incoming.Trash))
                {
                    token.ThrowIfCancellationRequested();
                    if (entry.Metadata.CoverRef is not { } reference) continue;
                    BackupCover asset = prepared.Manifest.Covers.Single(cover => cover.Path == ArchiveName(reference));
                    string file = Path.Combine(prepared.StagePath, Path.GetFileName(asset.Path));
                    if (new FileInfo(file).Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new IOException("Staged cover must not be a link.");
                    byte[] bytes;
                    using (var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        if (input.Length != asset.Length || input.Length > CoverStorageService.MaxBytes)
                            throw new InvalidDataException("Staged asset size changed after validation.");
                        bytes = new byte[(int)input.Length];
                        await input.ReadExactlyAsync(bytes, token);
                    }
                    if (bytes.Length != asset.Length || Hash(bytes) != asset.Sha256) throw new InvalidDataException("Staged asset changed after validation.");
                    string owned = await covers.ImportBytesAsync(entry.Id, bytes, token);
                    staged.Add(owned);
                    entry.Metadata.CoverRef = owned;
                }
                await repository.ReplaceAllAsync(incoming, current.Signature, token);
                staged.Clear(); // Ownership commits with DB success; no cancellation exception after commit.
                collection.ClearUndoAfterReplacement();
                return new BackupRestoreResult(incoming.Entries.Count, incoming.Trash.Count, recovery);
            }
            catch
            {
                // On an uncertain commit/read failure, retaining an orphan is safer than deleting a referenced cover.
                try
                {
                    AnimeLibrarySnapshot saved = await repository.GetSnapshotAsync();
                    var referenced = saved.Entries.Concat(saved.Trash).Select(entry => entry.Metadata.CoverRef).ToHashSet();
                    foreach (string reference in staged.Where(reference => !referenced.Contains(reference))) covers.RemoveUncommitted(reference);
                }
                catch (Exception cleanupError) { log.Error(cleanupError, "Staged covers retained for safe review after restore failure."); }
                throw;
            }
        });

    public async Task<string> PermanentlyDeleteAsync(AnimeEntry archived, CancellationToken token = default) =>
        await collection.WithExclusiveWriteAsync(async () =>
        {
            AnimeLibrarySnapshot snapshot = await repository.GetSnapshotAsync();
            AnimeEntry current = snapshot.Trash.SingleOrDefault(entry => entry.Id == archived.Id)
                ?? throw new KeyNotFoundException("Archived anime no longer exists.");
            if (current.Revision != archived.Revision) throw new AnimeConflictException("Archived anime changed; reload before deleting.");
            Directory.CreateDirectory(paths.BackupsPath);
            if (new DirectoryInfo(paths.BackupsPath).Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new IOException("Backups path must not be a link.");
            string recovery = Path.Combine(paths.BackupsPath, $"Before-Purge-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.zip");
            await ExportSnapshotAsync(snapshot, recovery, token);
            token.ThrowIfCancellationRequested();
            await repository.PermanentlyDeleteAsync(archived.Id, archived.Revision);
            collection.ClearUndoAfterReplacement();
            return recovery; // All covers remain; physical cleanup is preview-only.
        });

    public async Task<IReadOnlyList<string>> GetUnreferencedCoverPreviewAsync()
    {
        AnimeLibrarySnapshot snapshot = await repository.GetSnapshotAsync();
        var referenced = snapshot.Entries.Concat(snapshot.Trash).Select(entry => entry.Metadata.CoverRef).ToHashSet();
        return covers.ListOwnedReferences().Where(reference => !referenced.Contains(reference)).ToList();
    }
}
