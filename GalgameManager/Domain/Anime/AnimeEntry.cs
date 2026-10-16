using System.Text.Json;
using System.Text.Json.Serialization;

namespace LemonVN.Domain.Anime;

public sealed class AnimeEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int SchemaVersion { get; set; } = 1;
    public long Revision { get; set; }
    public AnimeMetadata Metadata { get; set; } = new();
    public AnimeRecord Record { get; set; } = new();
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public AnimeEntry Copy() => JsonSerializer.Deserialize<AnimeEntry>(JsonSerializer.Serialize(this))!;
}

public sealed class AnimeMetadata
{
    public string Title { get; set; } = string.Empty;
    public string? OriginalTitle { get; set; }
    public string? Description { get; set; }
    // Application-owned relative reference; never an external source filename.
    public string? CoverRef { get; set; }
    public int? TotalEpisodes { get; set; }
    public DateOnly? AirDate { get; set; }
    public string? BangumiId { get; set; }
    // Optional additive BSON field: older documents deserialize to an empty list.
    public List<string> AnimationStudios { get; set; } = [];
    public HashSet<string> UserOverrides { get; set; } = [];
}

public enum WatchStatus { Planned, Watching, Completed, OnHold, Dropped }

public sealed class AnimeRecord
{
    public WatchStatus WatchStatus { get; set; }
    public int? PersonalRating { get; set; }
    public string? Notes { get; set; }
    public List<string> Tags { get; set; } = [];
    public DateOnly? StartedAt { get; set; }
    public DateOnly? CompletedAt { get; set; }
    public List<EpisodeRecord> Episodes { get; set; } = [];
    [JsonIgnore] public int WatchedCount => Episodes.Count(episode => episode.IsWatched);
}

public sealed class EpisodeRecord
{
    public int EpisodeNumber { get; set; }
    public bool IsWatched { get; set; }
    public DateTime? WatchedAtUtc { get; set; }
    public string? Notes { get; set; }
}
