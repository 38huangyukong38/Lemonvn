using LemonVN.Contracts.Services;
using LemonVN.Domain.Anime;
using LemonVN.Helpers;

namespace LemonVN.ViewModels.Anime;

public sealed class AnimeCardViewModel(AnimeEntry entry, ICoverStorageService covers,
    Func<string, object[], string>? translate = null)
{
    private readonly Func<string, object[], string> _translate = translate ?? LemonLocalization.GetString;
    public Guid Id => entry.Id;
    public AnimeCardTarget Target => new(entry.Id, entry.Revision, entry.Metadata.Title,
        AnimeWatchStatusRules.GetStatus(entry), entry.Metadata.BangumiId);
    public string Title => entry.Metadata.Title;
    public string Status => _translate("Anime_Status_" + AnimeWatchStatusRules.GetStatus(entry), []);
    public string CoverSource => covers.GetDisplaySource(entry.Metadata.CoverRef);
    public string ProgressText => entry.Metadata.TotalEpisodes is { } total
        ? _translate("AnimeProgress_Known", [entry.Record.WatchedCount, total])
        : _translate("AnimeProgress_Unknown", [entry.Record.WatchedCount]);
    public bool HasKnownTotal => entry.Metadata.TotalEpisodes is not null;
    public double Percentage => entry.Metadata.TotalEpisodes is { } total ? Math.Min(100, entry.Record.WatchedCount * 100.0 / total) : 0;
    public string TagsText => string.Join(" · ", entry.Record.Tags.Take(3));
    public string RatingText => entry.Record.PersonalRating is { } rating ? $"{rating}/10" : string.Empty;
}

// Immutable action identity survives container recycling and awaits; never use a global selection.
public sealed record AnimeCardTarget(Guid Id, long Revision, string Title, WatchStatus Status, string? BangumiId);
public sealed record AnimeStatusChange(AnimeCardTarget Target, WatchStatus Status);
