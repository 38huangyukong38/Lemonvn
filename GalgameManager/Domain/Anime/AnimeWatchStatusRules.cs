namespace LemonVN.Domain.Anime;

// Display/query projection for old documents; no read-time database migration.
// Only an explicit hold/drop choice overrides episode-derived automatic states.
public static class AnimeWatchStatusRules
{
    public static WatchStatus GetStatus(AnimeEntry entry)
    {
        if (entry.Record.WatchStatus is WatchStatus.OnHold or WatchStatus.Dropped)
            return entry.Record.WatchStatus;
        int watched = 0;
        int withinTotal = 0;
        foreach (EpisodeRecord episode in entry.Record.Episodes)
        {
            if (!episode.IsWatched) continue;
            watched++;
            if (entry.Metadata.TotalEpisodes is { } total && episode.EpisodeNumber <= total)
                withinTotal++;
        }
        // Validated records have unique positive episode numbers. Out-of-range
        // episodes must never fill gaps in 1..TotalEpisodes.
        if (entry.Metadata.TotalEpisodes is { } count && count > 0 && withinTotal == count)
            return WatchStatus.Completed;
        return watched > 0 ? WatchStatus.Watching : WatchStatus.Planned;
    }
}
