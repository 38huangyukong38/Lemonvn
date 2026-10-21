using LemonVN.Domain.Anime;

namespace LemonVN.Application.Anime;

public enum AnimeSort { Updated, Added, Title, Rating, Progress, LastWatched }
public sealed record AnimeQuery(string Search = "", WatchStatus? Status = null, string? Tag = null,
    AnimeSort Sort = AnimeSort.Updated, bool? Descending = null, string? AnimationStudio = null,
    bool UnassignedStudio = false, bool OnlyWithWatchTime = false);

public sealed record AnimeLibraryRequest(WatchStatus? Status = null, string? AnimationStudio = null,
    bool UnassignedStudio = false, bool Recent = false);
public sealed record AnimeCategoryGroup(string? Studio, int Count, AnimeLibraryRequest Target);

public sealed class AnimeQueryService
{
    public IReadOnlyList<AnimeEntry> Apply(IEnumerable<AnimeEntry> entries, AnimeQuery query)
    {
        string search = query.Search.Trim();
        IEnumerable<AnimeEntry> filtered = entries.Where(entry =>
            (search.Length == 0 || entry.Metadata.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (entry.Metadata.OriginalTitle?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)) &&
            (query.Status is null || AnimeWatchStatusRules.GetStatus(entry) == query.Status) &&
            (query.Tag is null || entry.Record.Tags.Any(tag => string.Equals(tag.Trim(), query.Tag.Trim(), StringComparison.OrdinalIgnoreCase))) &&
            (query.AnimationStudio is null || entry.Metadata.AnimationStudios.Contains(query.AnimationStudio, StringComparer.OrdinalIgnoreCase)) &&
            (!query.UnassignedStudio || entry.Metadata.AnimationStudios.Count == 0) &&
            (!query.OnlyWithWatchTime || LastWatchedAt(entry) is not null));
        bool descending = query.Descending ?? query.Sort != AnimeSort.Title;
        IOrderedEnumerable<AnimeEntry> ordered = query.Sort switch
        {
            AnimeSort.Updated => Order(filtered, entry => entry.UpdatedAtUtc, descending),
            AnimeSort.Added => Order(filtered, entry => entry.CreatedAtUtc, descending),
            AnimeSort.Title => descending
                ? filtered.OrderByDescending(entry => entry.Metadata.Title, StringComparer.OrdinalIgnoreCase)
                : filtered.OrderBy(entry => entry.Metadata.Title, StringComparer.OrdinalIgnoreCase),
            AnimeSort.LastWatched => Then(filtered.OrderByDescending(entry => LastWatchedAt(entry).HasValue), LastWatchedAt, descending),
            // Missing values always follow known values in either direction.
            AnimeSort.Rating => Then(filtered.OrderByDescending(entry => entry.Record.PersonalRating.HasValue),
                entry => entry.Record.PersonalRating, descending),
            AnimeSort.Progress => Then(Then(filtered.OrderByDescending(entry => entry.Metadata.TotalEpisodes.HasValue),
                entry => entry.Metadata.TotalEpisodes is { } total ? Math.Min(1, entry.Record.WatchedCount / (double)total) : 0,
                descending), entry => entry.Record.WatchedCount, descending),
            _ => throw new ArgumentOutOfRangeException(nameof(query))
        };
        return ordered.ThenBy(entry => entry.Metadata.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Id).ToList();
    }

    private static IOrderedEnumerable<AnimeEntry> Order<T>(IEnumerable<AnimeEntry> entries,
        Func<AnimeEntry, T> key, bool descending) => descending ? entries.OrderByDescending(key) : entries.OrderBy(key);
    private static IOrderedEnumerable<AnimeEntry> Then<T>(IOrderedEnumerable<AnimeEntry> entries,
        Func<AnimeEntry, T> key, bool descending) => descending ? entries.ThenByDescending(key) : entries.ThenBy(key);

    public IReadOnlyList<string> GetTags(IEnumerable<AnimeEntry> entries) => entries.SelectMany(entry => entry.Record.Tags)
        .Select(tag => tag.Trim()).Where(tag => tag.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase)
        .Order(StringComparer.OrdinalIgnoreCase).ToList();

    public static DateTime? LastWatchedAt(AnimeEntry entry)
    {
        DateTime? latest = null;
        foreach (EpisodeRecord episode in entry.Record.Episodes)
            if (episode.IsWatched && episode.WatchedAtUtc is { Kind: DateTimeKind.Utc } time && time != default &&
                (latest is null || time > latest)) latest = time;
        return latest;
    }

    public IReadOnlyList<AnimeCategoryGroup> GetStudioGroups(IEnumerable<AnimeEntry> source)
    {
        List<AnimeEntry> entries = source.ToList();
        var groups = entries.SelectMany(entry => entry.Metadata.AnimationStudios.Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(studio => (Studio: studio, entry.Id)))
            .GroupBy(item => item.Studio, StringComparer.OrdinalIgnoreCase)
            .Select(group => new AnimeCategoryGroup(group.Select(item => item.Studio).Order(StringComparer.Ordinal).First(),
                group.Select(item => item.Id).Distinct().Count(),
                new(AnimationStudio: group.Select(item => item.Studio).Order(StringComparer.Ordinal).First())))
            .OrderBy(group => group.Studio, StringComparer.OrdinalIgnoreCase).ToList();
        int missing = entries.Count(entry => entry.Metadata.AnimationStudios.Count == 0);
        if (missing > 0) groups.Add(new(null, missing, new(UnassignedStudio: true)));
        return groups;
    }
}
