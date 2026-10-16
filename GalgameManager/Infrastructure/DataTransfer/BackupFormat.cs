using System.Text.Json;
using System.Text.Json.Serialization;
using LemonVN.Domain.Anime;

namespace LemonVN.Infrastructure.Backup;

public sealed class BackupManifest
{
    public int ExportFormatVersion { get; set; } = BackupFormat.CurrentVersion;
    public string Application { get; set; } = "LemonVN.Desktop";
    public string AppVersion { get; set; } = string.Empty;
    public DateTime ExportedAtUtc { get; set; }
    public int AnimeCount { get; set; }
    public int TrashCount { get; set; }
    public string AnimeSha256 { get; set; } = string.Empty;
    public string TrashSha256 { get; set; } = string.Empty;
    public List<BackupCover> Covers { get; set; } = [];
}

public sealed record BackupCover(string Path, long Length, string Sha256);
public sealed record BackupRestoreResult(int AnimeCount, int TrashCount, string RecoveryBackupPath);

public static class BackupFormat
{
    public const int CurrentVersion = 2;
    public const long MaxTotalBytes = 512L * 1024 * 1024;
    public const int MaxJsonBytes = 32 * 1024 * 1024;
    public const int MaxManifestBytes = 4 * 1024 * 1024;
    public const int MaxRecords = 10_000;
    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
        MaxDepth = 32
    };

    public static void CheckJson(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new InvalidDataException("Duplicate JSON property.");
                if (property.Name is "watchedCount" or "watchedEpisodes" or "currentEpisode")
                    throw new InvalidDataException("Backup must use episode records as the sole progress source.");
                CheckJson(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (JsonElement child in element.EnumerateArray()) CheckJson(child);
    }

    private static void Require(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object || names.Any(name => !element.TryGetProperty(name, out _)))
            throw new InvalidDataException("Missing required backup fields.");
    }

    public static void CheckManifest(JsonElement element)
    {
        CheckJson(element);
        Require(element, "exportFormatVersion", "application", "appVersion", "exportedAtUtc", "animeCount", "trashCount", "animeSha256", "trashSha256", "covers");
    }

    public static List<AnimeEntry> ReadEntries(byte[] bytes, int formatVersion = CurrentVersion)
    {
        if (formatVersion is not (1 or CurrentVersion)) throw new InvalidDataException("Unsupported backup format.");
        using JsonDocument document = JsonDocument.Parse(bytes, new() { MaxDepth = 32 });
        CheckJson(document.RootElement);
        if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() > MaxRecords)
            throw new InvalidDataException("Invalid or excessive anime count.");
        foreach (JsonElement entry in document.RootElement.EnumerateArray())
        {
            Require(entry, "id", "schemaVersion", "revision", "metadata", "record", "createdAtUtc", "updatedAtUtc");
            Require(entry.GetProperty("metadata"), "title");
            if (formatVersion == 1 && entry.GetProperty("metadata").TryGetProperty("animationStudios", out _))
                throw new InvalidDataException("Animation studios require backup format 2.");
            Require(entry.GetProperty("record"), "watchStatus", "tags", "episodes");
            foreach (JsonElement episode in entry.GetProperty("record").GetProperty("episodes").EnumerateArray())
                Require(episode, "episodeNumber", "isWatched");
        }
        List<AnimeEntry> entries = JsonSerializer.Deserialize<List<AnimeEntry>>(bytes, JsonOptions)
            ?? throw new InvalidDataException("Missing anime data.");
        foreach (AnimeEntry entry in entries)
        {
            AnimeValidation.Validate(entry);
            if (entry.Record.Tags.Any(tag => tag is null))
                throw new InvalidDataException("Backup tags must be text values.");
            // Reject invalid tag data before a restore can make library queries fail.
            // Validation does not normalize or discard fields from the imported record.
            _ = AnimeTagRules.Normalize(entry.Record.Tags);
            if (entry.Revision <= 0 || entry.Revision >= long.MaxValue - 100_000 || entry.CreatedAtUtc == default ||
                entry.UpdatedAtUtc == default || entry.CreatedAtUtc.Kind != DateTimeKind.Utc || entry.UpdatedAtUtc.Kind != DateTimeKind.Utc ||
                entry.Record.Episodes.Any(episode => episode.WatchedAtUtc is { Kind: not DateTimeKind.Utc }))
                throw new InvalidDataException("Invalid backup revision or UTC timestamp.");
        }
        return entries;
    }
}
