using LemonVN.Domain.Anime;

namespace LemonVN.Application.Anime;

// A projection of EpisodeRecord, never an additional persisted progress value.
public sealed record AnimeProgressState(int WatchedCount, int? TotalEpisodes, double Percentage,
    int? NextUnwatched, int? HighestWatched, int OutOfRangeCount, int LastDisplayEpisode);

public sealed record EpisodeState(int EpisodeNumber, bool IsWatched, DateTime? WatchedAtUtc,
    string? Notes, bool IsOutOfRange);

public sealed class AnimeProgressService(AnimeCollectionService collection)
{
    public const int MaxBatchEpisodes = 10_000;
    public const int PageSize = 50;
    private readonly SemaphoreSlim _operations = new(1, 1);

    public AnimeProgressState GetProgress(AnimeEntry entry)
    {
        AnimeValidation.Validate(entry);
        int[] watched = entry.Record.Episodes.Where(episode => episode.IsWatched)
            .Select(episode => episode.EpisodeNumber).Order().ToArray();
        long next = 1;
        foreach (int number in watched)
        {
            if (number != next) break;
            next++;
        }
        int? nextEpisode = next > int.MaxValue || (entry.Metadata.TotalEpisodes is { } total && next > total)
            ? null : (int)next;
        int highestRecord = entry.Record.Episodes.Select(episode => episode.EpisodeNumber).DefaultIfEmpty(0).Max();
        int lastDisplay = entry.Metadata.TotalEpisodes is { } known
            ? Math.Max(known, highestRecord) : (int)Math.Min(int.MaxValue, (long)highestRecord + 1);
        int outside = entry.Metadata.TotalEpisodes is { } limit
            ? entry.Record.Episodes.Count(episode => episode.EpisodeNumber > limit) : 0;
        double percentage = entry.Metadata.TotalEpisodes is { } count
            ? Math.Min(100, watched.Length * 100.0 / count) : 0;
        return new(watched.Length, entry.Metadata.TotalEpisodes, percentage, nextEpisode,
            watched.Length == 0 ? null : watched[^1], outside, lastDisplay);
    }

    public IReadOnlyList<EpisodeState> GetEpisodeStates(AnimeEntry entry, int firstEpisode = 1)
    {
        AnimeProgressState progress = GetProgress(entry);
        if (firstEpisode <= 0 || firstEpisode > progress.LastDisplayEpisode)
            throw new AnimeValidationException("Invalid episode page.");
        Dictionary<int, EpisodeRecord> recorded = entry.Record.Episodes.ToDictionary(episode => episode.EpisodeNumber);
        var states = new List<EpisodeState>();
        long last = Math.Min(progress.LastDisplayEpisode, (long)firstEpisode + PageSize - 1);
        for (long number = firstEpisode; number <= last; number++)
        {
            recorded.TryGetValue((int)number, out EpisodeRecord? episode);
            states.Add(new((int)number, episode?.IsWatched ?? false, episode?.WatchedAtUtc, episode?.Notes,
                entry.Metadata.TotalEpisodes is { } total && number > total));
        }
        return states;
    }

    public Task<AnimeEntry> MarkNextUnwatchedAsync(Guid id, long expectedRevision) =>
        ChangeAsync(id, expectedRevision, entry =>
        {
            int number = GetProgress(entry).NextUnwatched
                ?? throw new AnimeValidationException("There is no next unwatched episode within the total.");
            return SetWatched(entry, number, true);
        });

    public Task<AnimeEntry> SetEpisodeWatchedAsync(Guid id, long expectedRevision, int episodeNumber, bool watched) =>
        ChangeAsync(id, expectedRevision, entry => SetWatched(entry, episodeNumber, watched));

    public Task<AnimeEntry> MarkWatchedThroughAsync(Guid id, long expectedRevision, int episodeNumber)
    {
        // Bound an explicit batch, rather than allocating an unbounded document/UI.
        if (episodeNumber is <= 0 or > MaxBatchEpisodes)
            throw new AnimeValidationException($"Batch episode must be between 1 and {MaxBatchEpisodes}.");
        return ChangeAsync(id, expectedRevision, entry =>
        {
            if (entry.Metadata.TotalEpisodes is { } total && episodeNumber > total)
                throw new AnimeValidationException("Batch episode exceeds the known total.");
            bool changed = false;
            Dictionary<int, EpisodeRecord> recorded = entry.Record.Episodes.ToDictionary(episode => episode.EpisodeNumber);
            DateTime now = DateTime.UtcNow;
            for (int number = 1; number <= episodeNumber; number++)
            {
                if (!recorded.TryGetValue(number, out EpisodeRecord? episode))
                {
                    episode = new() { EpisodeNumber = number };
                    entry.Record.Episodes.Add(episode);
                }
                if (episode.IsWatched) continue; // Preserve original watch dates and notes.
                episode.IsWatched = true;
                episode.WatchedAtUtc = now;
                changed = true;
            }
            return changed;
        });
    }

    public Task<AnimeEntry> UnmarkHighestWatchedAsync(Guid id, long expectedRevision) =>
        ChangeAsync(id, expectedRevision, entry =>
        {
            int? highest = GetProgress(entry).HighestWatched;
            return highest is { } number && SetWatched(entry, number, false);
        });

    private static bool SetWatched(AnimeEntry entry, int number, bool watched)
    {
        if (number <= 0) throw new AnimeValidationException("Episode number must be positive.");
        EpisodeRecord? episode = entry.Record.Episodes.SingleOrDefault(item => item.EpisodeNumber == number);
        if (watched && episode is null && entry.Metadata.TotalEpisodes is { } total && number > total)
            throw new AnimeValidationException("Cannot create an episode above the known total.");
        if (episode is null)
        {
            if (!watched) return false; // Unoperated episodes stay sparse.
            episode = new() { EpisodeNumber = number };
            entry.Record.Episodes.Add(episode);
        }
        if (episode.IsWatched == watched) return false;
        episode.IsWatched = watched;
        episode.WatchedAtUtc = watched ? DateTime.UtcNow : null;
        return true;
    }

    private async Task<AnimeEntry> ChangeAsync(Guid id, long expectedRevision, Func<AnimeEntry, bool> change)
    {
        await _operations.WaitAsync();
        try
        {
            AnimeEntry entry = await collection.GetByIdAsync(id) ?? throw new KeyNotFoundException("Anime no longer exists.");
            if (entry.Revision != expectedRevision)
                throw new AnimeConflictException("The anime changed; reload before changing progress.");
            AnimeValidation.Validate(entry);
            if (!change(entry)) return entry;
            // M2 checks this revision again at commit, including changes made outside this service.
            return await collection.UpdateRecordAsync(id, expectedRevision, entry.Record);
        }
        finally
        {
            _operations.Release();
        }
    }
}
