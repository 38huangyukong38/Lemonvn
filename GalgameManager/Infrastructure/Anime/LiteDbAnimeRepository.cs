using LemonVN.Contracts.Services;
using LemonVN.Domain.Anime;
using LemonVN.Services;
using LiteDB;

namespace LemonVN.Infrastructure.Anime;

public sealed class LiteDbAnimeRepository(AppDatabaseService database) : IAnimeRepository
{
    private static ILiteCollection<AnimeEntry> Entries(LiteDatabase db) => db.GetCollection<AnimeEntry>("anime");
    private static long RevisionFloor(LiteDatabase db) => db.GetCollection("app_info").FindById("revision_floor")?["value"].AsInt64 ?? 0;
    private static void SaveFloor(LiteDatabase db, long value) => db.GetCollection("app_info").Upsert(new BsonDocument
        { ["_id"] = "revision_floor", ["value"] = value });

    private static void ValidateLoaded(AnimeEntry entry)
    {
        if (entry.SchemaVersion != 1) throw new InvalidDataException("Unsupported anime schema version. Data was preserved.");
        AnimeValidation.Validate(entry);
    }

    public Task<IReadOnlyList<AnimeEntry>> GetAllAsync() => Task.Run<IReadOnlyList<AnimeEntry>>(() =>
        database.Read(db =>
        {
            List<AnimeEntry> entries = Entries(db).FindAll().OrderByDescending(entry => entry.CreatedAtUtc).ToList();
            entries.ForEach(ValidateLoaded);
            return entries;
        }));

    public Task<AnimeEntry?> GetByIdAsync(Guid id) => Task.Run(() => database.Read(db =>
    {
        AnimeEntry? entry = Entries(db).FindById(id);
        if (entry is not null) ValidateLoaded(entry);
        return entry;
    }));

    private static void EnsureExternalIdUnique(ILiteCollection<AnimeEntry> entries, AnimeEntry candidate)
    {
        if (candidate.Metadata.BangumiId is { } externalId &&
            entries.Exists(entry => entry.Metadata.BangumiId == externalId && entry.Id != candidate.Id))
            throw new AnimeConflictException("That Bangumi subject is already in the library.");
    }

    public Task<AnimeEntry> AddAsync(AnimeEntry entry)
    {
        AnimeEntry snapshot = entry.Copy();
        AnimeValidation.Validate(snapshot);
        return Task.Run(() => database.Write(db =>
        {
            ILiteCollection<AnimeEntry> entries = Entries(db);
            entries.EnsureIndex("bangumi_id", "$.Metadata.BangumiId");
            if (entries.Exists(item => item.Id == snapshot.Id)) throw new AnimeConflictException("The anime ID already exists.");
            if (db.GetCollection<AnimeEntry>("anime_trash").Exists(item => item.Id == snapshot.Id))
                throw new AnimeConflictException("That anime ID is in the recycle bin; restore it instead.");
            EnsureExternalIdUnique(entries, snapshot);
            snapshot.Metadata.Title = snapshot.Metadata.Title.Trim();
            snapshot.CreatedAtUtc = snapshot.UpdatedAtUtc = DateTime.UtcNow;
            snapshot.Revision = checked(RevisionFloor(db) + 1);
            entries.Insert(snapshot);
            return snapshot;
        }));
    }

    public Task<AnimeEntry> UpdateAsync(AnimeEntry entry)
    {
        AnimeEntry snapshot = entry.Copy();
        AnimeValidation.Validate(snapshot);
        return Task.Run(() => database.Write(db =>
        {
            ILiteCollection<AnimeEntry> entries = Entries(db);
            AnimeEntry current = entries.FindById(snapshot.Id) ?? throw new KeyNotFoundException("Anime no longer exists.");
            ValidateLoaded(current);
            if (current.Revision != snapshot.Revision) throw new AnimeConflictException("The anime changed; reload it before saving.");
            EnsureExternalIdUnique(entries, snapshot);
            snapshot.Metadata.Title = snapshot.Metadata.Title.Trim();
            snapshot.CreatedAtUtc = current.CreatedAtUtc;
            snapshot.UpdatedAtUtc = DateTime.UtcNow;
            snapshot.Revision = checked(current.Revision + 1);
            if (!entries.Update(snapshot)) throw new IOException("The anime update was not saved.");
            return snapshot;
        }));
    }

    public Task DeleteAsync(Guid id, long expectedRevision) => Task.Run(() => database.Write(db =>
    {
        AnimeEntry current = Entries(db).FindById(id) ?? throw new KeyNotFoundException("Anime no longer exists.");
        ValidateLoaded(current);
        if (current.Revision != expectedRevision) throw new AnimeConflictException("The anime changed; reload it before deleting.");
        // Recoverable deletion. Covers remain referenced by the archived snapshot.
        db.GetCollection<AnimeEntry>("anime_trash").Upsert(current);
        if (!Entries(db).Delete(id)) throw new IOException("The anime was not deleted.");
        return true;
    }));

    public Task<AnimeEntry> RestoreAsync(Guid id) => RestoreInternalAsync(id, null);
    public Task<AnimeEntry> RestoreAsync(Guid id, long expectedRevision) => RestoreInternalAsync(id, expectedRevision);

    private Task<AnimeEntry> RestoreInternalAsync(Guid id, long? expectedRevision) => Task.Run(() => database.Write(db =>
    {
        ILiteCollection<AnimeEntry> trash = db.GetCollection<AnimeEntry>("anime_trash");
        AnimeEntry entry = trash.FindById(id) ?? throw new KeyNotFoundException("No recoverable anime found.");
        ValidateLoaded(entry);
        if (expectedRevision is { } expected && entry.Revision != expected)
            throw new AnimeConflictException("The archived anime changed; reload before restoring.");
        if (Entries(db).Exists(item => item.Id == id)) throw new AnimeConflictException("The anime already exists.");
        EnsureExternalIdUnique(Entries(db), entry);
        entry.Revision = checked(Math.Max(entry.Revision, RevisionFloor(db)) + 1);
        entry.UpdatedAtUtc = DateTime.UtcNow;
        Entries(db).Insert(entry);
        trash.Delete(id);
        return entry;
    }));

    public Task<IReadOnlyList<AnimeEntry>> GetTrashAsync() => Task.Run<IReadOnlyList<AnimeEntry>>(() => database.Read(db =>
    {
        List<AnimeEntry> entries = db.GetCollection<AnimeEntry>("anime_trash").FindAll()
            .OrderByDescending(entry => entry.UpdatedAtUtc).ThenBy(entry => entry.Id).ToList();
        entries.ForEach(ValidateLoaded);
        return entries;
    }));

    public Task PermanentlyDeleteAsync(Guid id, long expectedRevision) => Task.Run(() => database.Write(db =>
    {
        ILiteCollection<AnimeEntry> trash = db.GetCollection<AnimeEntry>("anime_trash");
        AnimeEntry entry = trash.FindById(id) ?? throw new KeyNotFoundException("Archived anime no longer exists.");
        ValidateLoaded(entry);
        if (entry.Revision != expectedRevision) throw new AnimeConflictException("The archived anime changed; reload it.");
        // Remember removed revisions so a later import/re-add cannot accept an old editor snapshot.
        SaveFloor(db, Math.Max(RevisionFloor(db), entry.Revision));
        if (!trash.Delete(id)) throw new IOException("Archived anime was not deleted.");
        return true;
    }));

    private static AnimeLibrarySnapshot Snapshot(LiteDatabase db)
    {
        var snapshot = new AnimeLibrarySnapshot(Entries(db).FindAll().ToList(),
            db.GetCollection<AnimeEntry>("anime_trash").FindAll().ToList());
        foreach (AnimeEntry entry in snapshot.Entries.Concat(snapshot.Trash)) ValidateLoaded(entry);
        snapshot.Validate();
        return snapshot;
    }

    public Task<AnimeLibrarySnapshot> GetSnapshotAsync() => Task.Run(() => database.Read(Snapshot));

    public Task ReplaceAllAsync(AnimeLibrarySnapshot snapshot, string expectedSignature, CancellationToken cancellationToken)
    {
        var incoming = new AnimeLibrarySnapshot(snapshot.Entries.Select(entry => entry.Copy()).ToList(),
            snapshot.Trash.Select(entry => entry.Copy()).ToList());
        incoming.Validate();
        return Task.Run(() => database.Write(db =>
        {
            AnimeLibrarySnapshot current = Snapshot(db);
            if (current.Signature != expectedSignature)
                throw new AnimeConflictException("Library changed after preview; select and confirm the backup again.");
            cancellationToken.ThrowIfCancellationRequested();
            long highest = current.Entries.Concat(current.Trash).Concat(incoming.Entries).Concat(incoming.Trash)
                .Select(entry => entry.Revision).Append(RevisionFloor(db)).Max();
            long restoredRevision = checked(highest + 1);
            Entries(db).DeleteAll();
            ILiteCollection<AnimeEntry> trash = db.GetCollection<AnimeEntry>("anime_trash");
            trash.DeleteAll();
            foreach (AnimeEntry entry in incoming.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                entry.Revision = restoredRevision;
                Entries(db).Insert(entry);
            }
            foreach (AnimeEntry entry in incoming.Trash)
            {
                cancellationToken.ThrowIfCancellationRequested();
                entry.Revision = restoredRevision;
                trash.Insert(entry);
            }
            SaveFloor(db, restoredRevision);
            cancellationToken.ThrowIfCancellationRequested();
            return true;
        }));
    }
}
