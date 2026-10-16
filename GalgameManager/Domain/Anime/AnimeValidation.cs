using System.Text.RegularExpressions;

namespace LemonVN.Domain.Anime;

public sealed class AnimeValidationException(string message) : ArgumentException(message);
public sealed class AnimeConflictException(string message) : InvalidOperationException(message);

public static class AnimeValidation
{
    public static void Validate(AnimeEntry entry)
    {
        if (entry.Id == Guid.Empty || entry.SchemaVersion != 1 || entry.Metadata is null || entry.Record is null)
            throw new AnimeValidationException("Invalid anime identity or data format.");
        if (string.IsNullOrWhiteSpace(entry.Metadata.Title))
            throw new AnimeValidationException("Title is required.");
        AnimationStudioRules.Validate(entry.Metadata.AnimationStudios);
        if (entry.Metadata.TotalEpisodes is <= 0)
            throw new AnimeValidationException("Total episodes must be unknown or a positive integer.");
        if (entry.Metadata.CoverRef is { } cover && !Regex.IsMatch(cover,
                $"^Covers/{entry.Id:N}_[0-9a-f]{{32}}\\.(png|jpg)$", RegexOptions.CultureInvariant))
            throw new AnimeValidationException("Cover must be an owned relative reference for this anime.");
        if (entry.Record.PersonalRating is < 0 or > 10)
            throw new AnimeValidationException("Personal rating must be empty or an integer from 0 to 10.");
        if (!Enum.IsDefined(entry.Record.WatchStatus))
            throw new AnimeValidationException("Unknown watch status.");
        if (entry.Record.Episodes is null || entry.Record.Tags is null || entry.Metadata.UserOverrides is null)
            throw new AnimeValidationException("Incomplete personal record.");
        if (entry.Record.Episodes.Any(episode => episode is null || episode.EpisodeNumber <= 0) ||
            entry.Record.Episodes.GroupBy(episode => episode.EpisodeNumber).Any(group => group.Count() > 1))
            throw new AnimeValidationException("Episode numbers must be positive and unique within one anime.");
        if (entry.Metadata.BangumiId is { } externalId &&
            (!long.TryParse(externalId, out long number) || number <= 0))
            throw new AnimeValidationException("Invalid optional Bangumi subject ID.");
    }
}
