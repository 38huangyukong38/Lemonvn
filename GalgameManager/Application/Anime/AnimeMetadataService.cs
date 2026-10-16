using System.Security.Cryptography;
using LemonVN.Contracts.Services;
using LemonVN.Domain.Anime;
using LemonVN.Infrastructure.Bangumi;

namespace LemonVN.Application.Anime;

public enum MetadataChangeKind { Update, Protected, Missing, Unchanged }
public sealed record AnimeMetadataChange(string Field, string Local, string Remote, MetadataChangeKind Kind);
public sealed record AnimeLookupRequest(Guid? ExistingId = null, string Keyword = "", bool Refresh = false);
public sealed class AnimeAlreadyCollectedException(Guid existingId) : InvalidOperationException("That Bangumi subject is already collected.")
{
    public Guid ExistingId { get; } = existingId;
}

public sealed class AnimeMetadataPreview
{
    public Guid? ExistingId { get; }
    public long ExpectedRevision { get; }
    public BangumiAnime Source { get; }
    public IReadOnlyList<AnimeMetadataChange> Changes { get; }
    public bool HasOutOfRangeEpisodes { get; }
    public bool CoverUnavailable { get; }
    internal AnimeMetadata Proposed { get; }
    internal byte[]? CoverBytes { get; }
    internal AnimeMetadataPreview(Guid? id, long revision, BangumiAnime source, AnimeMetadata proposed,
        byte[]? cover, IReadOnlyList<AnimeMetadataChange> changes, bool outside, bool unavailable)
    {
        ExistingId = id; ExpectedRevision = revision; Source = source; Proposed = proposed;
        CoverBytes = cover; Changes = changes; HasOutOfRangeEpisodes = outside; CoverUnavailable = unavailable;
    }
}

public sealed class AnimeMetadataService(IBangumiAnimeProvider provider, IRemoteAnimeCoverCache remoteCovers,
    AnimeCollectionService collection, ICoverStorageService localCovers)
{
    public async Task<AnimeEntry?> FindExistingAsync(string subjectId, Guid? except = null)
    {
        string id = BangumiAnimeProvider.CanonicalId(subjectId);
        return (await collection.GetAllAsync()).FirstOrDefault(entry => entry.Id != except &&
            long.TryParse(entry.Metadata.BangumiId, out long existing) && existing.ToString() == id);
    }

    public async Task<AnimeMetadataPreview> PrepareAsync(string subjectId, Guid? existingId, bool fresh, CancellationToken token)
    {
        AnimeEntry? before = existingId is null ? null : await collection.GetByIdAsync(existingId.Value)
            ?? throw new KeyNotFoundException("Anime no longer exists.");
        AnimeEntry? duplicate = await FindExistingAsync(subjectId, existingId);
        if (duplicate is not null) throw new AnimeAlreadyCollectedException(duplicate.Id);
        BangumiAnime source = await provider.GetAnimeDetailsAsync(subjectId, fresh, token);
        AnimeMetadata proposed = before?.Copy().Metadata ?? new();
        proposed.BangumiId = source.Id;
        var changes = new List<AnimeMetadataChange>();
        void Merge<T>(string field, T local, T remote, Action<T> assign)
        {
            bool missing = remote is null || remote is string text && string.IsNullOrWhiteSpace(text);
            MetadataChangeKind kind = proposed.UserOverrides.Contains(field) ? MetadataChangeKind.Protected
                : missing ? MetadataChangeKind.Missing : Equals(local, remote) ? MetadataChangeKind.Unchanged : MetadataChangeKind.Update;
            changes.Add(new(field, local?.ToString() ?? string.Empty, remote?.ToString() ?? string.Empty, kind));
            if (kind == MetadataChangeKind.Update) assign(remote);
        }
        Merge("Title", proposed.Title, source.Title, value => proposed.Title = value);
        Merge("OriginalTitle", proposed.OriginalTitle, source.OriginalTitle, value => proposed.OriginalTitle = value);
        Merge("Description", proposed.Description, source.Description, value => proposed.Description = value);
        Merge("AirDate", proposed.AirDate, source.AirDate, value => proposed.AirDate = value);
        Merge("TotalEpisodes", proposed.TotalEpisodes, source.TotalEpisodes, value => proposed.TotalEpisodes = value);
        if (source.AnimationStudios.Count > 0 || proposed.AnimationStudios.Count > 0 || proposed.UserOverrides.Contains("AnimationStudios"))
        {
            List<string> studios = AnimationStudioRules.Normalize(source.AnimationStudios);
            MetadataChangeKind kind = proposed.UserOverrides.Contains("AnimationStudios") ? MetadataChangeKind.Protected
                : studios.Count == 0 ? MetadataChangeKind.Missing
                : studios.SequenceEqual(proposed.AnimationStudios, StringComparer.Ordinal) ? MetadataChangeKind.Unchanged : MetadataChangeKind.Update;
            changes.Add(new("AnimationStudios", string.Join(" · ", proposed.AnimationStudios), string.Join(" · ", studios), kind));
            if (kind == MetadataChangeKind.Update) proposed.AnimationStudios = studios;
        }
        byte[]? cover = null;
        bool unavailable = false;
        MetadataChangeKind coverKind;
        if (proposed.UserOverrides.Contains("CoverRef")) coverKind = MetadataChangeKind.Protected;
        else
        {
            try
            {
                cover = await remoteCovers.GetBytesAsync(source.CoverUri, token, fresh);
                if (cover is not null && proposed.CoverRef is not null)
                {
                    try
                    {
                        byte[] old = await localCovers.ReadOwnedAsync(proposed.CoverRef, token);
                        if (SHA256.HashData(old).SequenceEqual(SHA256.HashData(cover))) cover = null;
                    }
                    catch (IOException) { /* A validated new image can replace a missing owned image. */ }
                }
                coverKind = cover is not null ? MetadataChangeKind.Update : source.CoverUri is null ? MetadataChangeKind.Missing : MetadataChangeKind.Unchanged;
                unavailable = source.CoverUri is null;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                coverKind = MetadataChangeKind.Missing; unavailable = true; cover = null;
            }
        }
        token.ThrowIfCancellationRequested();
        changes.Add(new("CoverRef", proposed.CoverRef ?? string.Empty, source.CoverUri?.AbsoluteUri ?? string.Empty, coverKind));
        if (before is not null)
        {
            AnimeEntry latest = await collection.GetByIdAsync(before.Id) ?? throw new KeyNotFoundException("Anime no longer exists.");
            if (latest.Revision != before.Revision) throw new AnimeConflictException("The anime changed during the request; confirm again.");
        }
        bool outside = before is not null && proposed.TotalEpisodes is { } total && before.Record.Episodes.Any(episode => episode.EpisodeNumber > total);
        return new(existingId, before?.Revision ?? 0, source, proposed, cover, changes, outside, unavailable);
    }

    public async Task<AnimeEntry> CommitAsync(AnimeMetadataPreview preview, CancellationToken token)
    {
        AnimeEntry? duplicate = await FindExistingAsync(preview.Source.Id, preview.ExistingId);
        if (duplicate is not null) throw new AnimeAlreadyCollectedException(duplicate.Id);
        return await collection.SaveProviderMetadataAsync(preview.ExistingId, preview.ExpectedRevision,
            preview.Proposed, preview.CoverBytes, token);
    }
}
