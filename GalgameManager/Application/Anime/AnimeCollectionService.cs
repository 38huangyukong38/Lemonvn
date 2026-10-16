using LemonVN.Contracts.Services;
using LemonVN.Domain.Anime;
using LemonVN.Services;

namespace LemonVN.Application.Anime;

public sealed class AnimeCollectionService(IAnimeRepository repository, ICoverStorageService covers, AppLogService log)
{
    private readonly SemaphoreSlim _writes = new(1, 1);
    public Guid? LastDeletedId { get; private set; }
    public Task<IReadOnlyList<AnimeEntry>> GetAllAsync() => repository.GetAllAsync();
    public Task<AnimeEntry?> GetByIdAsync(Guid id) => repository.GetByIdAsync(id);

    // Backup/recycle operations share the existing write gate; no second DB lifecycle.
    public async Task<T> WithExclusiveWriteAsync<T>(Func<Task<T>> operation)
    {
        await _writes.WaitAsync();
        try { return await operation(); }
        finally { _writes.Release(); }
    }

    public void ClearUndoAfterReplacement() => LastDeletedId = null;

    public async Task<AnimeEntry> SaveAsync(AnimeEntry draft, bool isNew, string? selectedCover = null, bool removeCover = false)
    {
        AnimeEntry snapshot = draft.Copy();
        if (snapshot.Record?.Tags is { } tags) snapshot.Record.Tags = AnimeTagRules.Normalize(tags);
        AnimeMetadata metadata = snapshot.Metadata ?? throw new AnimeValidationException("Missing anime metadata.");
        if (metadata.AnimationStudios is { } studios) metadata.AnimationStudios = AnimationStudioRules.Normalize(studios);
        snapshot.Metadata = metadata;
        AnimeValidation.Validate(snapshot);
        await _writes.WaitAsync();
        string? stagedCover = null;
        try
        {
            AnimeEntry? current = isNew ? null : await repository.GetByIdAsync(snapshot.Id)
                ?? throw new KeyNotFoundException("Anime no longer exists.");
            // The UI cannot submit arbitrary external cover references.
            snapshot.Metadata.CoverRef = removeCover ? null : current?.Metadata.CoverRef;
            if (!string.IsNullOrWhiteSpace(selectedCover))
                snapshot.Metadata.CoverRef = stagedCover = await covers.ImportAsync(snapshot.Id, selectedCover);
            if (current is null)
            {
                snapshot.Metadata.UserOverrides.UnionWith(new[] { "Title", "OriginalTitle", "Description", "TotalEpisodes", "AirDate", "CoverRef" });
                if (snapshot.Metadata.AnimationStudios.Count > 0) snapshot.Metadata.UserOverrides.Add("AnimationStudios");
            }
            else
            {
                snapshot.Metadata.UserOverrides.UnionWith(current.Metadata.UserOverrides);
                ProtectChanged(snapshot.Metadata, current.Metadata, "Title", snapshot.Metadata.Title.Trim(), current.Metadata.Title);
                ProtectChanged(snapshot.Metadata, current.Metadata, "OriginalTitle", snapshot.Metadata.OriginalTitle, current.Metadata.OriginalTitle);
                ProtectChanged(snapshot.Metadata, current.Metadata, "Description", snapshot.Metadata.Description, current.Metadata.Description);
                ProtectChanged(snapshot.Metadata, current.Metadata, "TotalEpisodes", snapshot.Metadata.TotalEpisodes, current.Metadata.TotalEpisodes);
                ProtectChanged(snapshot.Metadata, current.Metadata, "AirDate", snapshot.Metadata.AirDate, current.Metadata.AirDate);
                if (!snapshot.Metadata.AnimationStudios.SequenceEqual(current.Metadata.AnimationStudios, StringComparer.Ordinal))
                    snapshot.Metadata.UserOverrides.Add("AnimationStudios");
                if (removeCover || stagedCover is not null) snapshot.Metadata.UserOverrides.Add("CoverRef");
            }
            AnimeEntry result = isNew ? await repository.AddAsync(snapshot) : await repository.UpdateAsync(snapshot);
            stagedCover = null; // Commit ownership only after DB success.
            return result;
        }
        catch (Exception error)
        {
            if (stagedCover is not null)
            {
                try { covers.RemoveUncommitted(stagedCover); }
                catch (Exception cleanupError) { log.Error(cleanupError, "Uncommitted cover retained for safe cleanup."); }
            }
            log.Error(error, "Anime save failed; existing data and covers preserved.");
            throw;
        }
        finally { _writes.Release(); }
    }

    private static void ProtectChanged(AnimeMetadata draft, AnimeMetadata current, string field, object? value, object? oldValue)
    {
        if (!Equals(value, oldValue)) draft.UserOverrides.Add(field);
    }

    // The network module supplies metadata only. Personal records are always copied
    // from the latest persisted entry, and the expected revision still gates commit.
    public async Task<AnimeEntry> SaveProviderMetadataAsync(Guid? id, long expectedRevision, AnimeMetadata metadata,
        byte[]? image, CancellationToken token)
    {
        await _writes.WaitAsync(token);
        string? staged = null;
        Guid owner = id ?? Guid.NewGuid();
        try
        {
            AnimeEntry? current = id is null ? null : await repository.GetByIdAsync(owner)
                ?? throw new KeyNotFoundException("Anime no longer exists.");
            if (current is not null && current.Revision != expectedRevision)
                throw new AnimeConflictException("The anime changed; reload and confirm metadata again.");
            AnimeEntry candidate = current?.Copy() ?? new() { Id = owner };
            candidate.Metadata = new AnimeEntry { Metadata = metadata }.Copy().Metadata;
            candidate.Metadata.CoverRef = current?.Metadata.CoverRef;
            AnimeValidation.Validate(candidate);
            string? externalId = candidate.Metadata.BangumiId;
            if (externalId is not null)
            {
                var entries = await repository.GetAllAsync();
                if (entries.Any(entry => entry.Id != owner && long.TryParse(entry.Metadata.BangumiId, out long existing) &&
                        long.TryParse(externalId, out long selected) && existing == selected))
                    throw new AnimeConflictException("That Bangumi subject is already in the library.");
            }
            token.ThrowIfCancellationRequested();
            if (image is null && current is not null &&
                System.Text.Json.JsonSerializer.Serialize(candidate.Metadata) == System.Text.Json.JsonSerializer.Serialize(current.Metadata))
                return current;
            if (image is not null) candidate.Metadata.CoverRef = staged = await covers.ImportBytesAsync(owner, image, token);
            token.ThrowIfCancellationRequested();
            AnimeEntry saved = current is null ? await repository.AddAsync(candidate) : await repository.UpdateAsync(candidate);
            staged = null;
            return saved;
        }
        catch
        {
            if (staged is not null)
            {
                try
                {
                    AnimeEntry? persisted = await repository.GetByIdAsync(owner);
                    if (persisted?.Metadata.CoverRef != staged) covers.RemoveUncommitted(staged);
                }
                catch (Exception cleanupError) { log.Error(cleanupError, "Provider cover retained after uncertain write failure."); }
            }
            throw;
        }
        finally { _writes.Release(); }
    }

    public async Task<AnimeEntry> UpdateRecordAsync(Guid id, long expectedRevision, AnimeRecord record)
    {
        await _writes.WaitAsync();
        try
        {
            AnimeEntry current = await repository.GetByIdAsync(id) ?? throw new KeyNotFoundException("Anime no longer exists.");
            current.Revision = expectedRevision;
            current.Record = new AnimeEntry { Record = record }.Copy().Record;
            return await repository.UpdateAsync(current);
        }
        finally { _writes.Release(); }
    }

    public async Task DeleteAsync(AnimeEntry entry)
    {
        await _writes.WaitAsync();
        try
        {
            await repository.DeleteAsync(entry.Id, entry.Revision);
            LastDeletedId = entry.Id;
            // Never remove original images or covers referenced by recovery snapshots.
        }
        finally { _writes.Release(); }
    }

    public async Task RestoreLastDeletedAsync()
    {
        await _writes.WaitAsync();
        try
        {
            await repository.RestoreAsync(LastDeletedId ?? throw new InvalidOperationException("No deletion to undo."));
            LastDeletedId = null;
        }
        finally { _writes.Release(); }
    }

    public async Task RestoreArchivedAsync(AnimeEntry archived)
    {
        await _writes.WaitAsync();
        try
        {
            await repository.RestoreAsync(archived.Id, archived.Revision);
            if (LastDeletedId == archived.Id) LastDeletedId = null;
        }
        finally { _writes.Release(); }
    }
}
