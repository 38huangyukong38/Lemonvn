using System.Globalization;
using System.Text.Json;
using LemonVN.Contracts.Services;
using LemonVN.Domain.Anime;

namespace LemonVN.Infrastructure.Bangumi;

public sealed class BangumiAnimeProvider(BangumiHttpTransport transport, Func<DateTimeOffset>? now = null) : IBangumiAnimeProvider
{
    public const int PageSize = 20;
    private readonly Func<DateTimeOffset> _now = now ?? (() => DateTimeOffset.UtcNow);
    private readonly SemaphoreSlim _queries = new(1, 1);
    private readonly Dictionary<string, (DateTimeOffset Time, BangumiSearchResults Value)> _search = new();
    private readonly Dictionary<string, (DateTimeOffset Time, BangumiAnime Value)> _details = new();
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    public static string CanonicalId(string id) => long.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out long value) && value > 0
        ? value.ToString(CultureInfo.InvariantCulture) : throw new ArgumentException("Invalid Bangumi subject ID.");

    public async Task<BangumiSearchResults> SearchAnimeAsync(string keyword, int offset, CancellationToken token)
    {
        keyword = keyword.Trim();
        if (keyword.Length is 0 or > 200 || keyword.Any(char.IsControl) || offset < 0 || offset > 100_000)
            throw new ArgumentException("Search needs 1–200 characters and a valid page.");
        await _queries.WaitAsync(token);
        try
        {
            string key = keyword + "\n" + offset;
            if (_search.TryGetValue(key, out var cached) && _now() - cached.Time < Lifetime) return cached.Value;
            string body = JsonSerializer.Serialize(new { keyword, sort = "match", filter = new { type = new[] { 2 } } });
            byte[] bytes = await transport.GetJsonAsync(new(BangumiNetworkPolicy.ApiRoot, $"v0/search/subjects?limit={PageSize}&offset={offset}"), body, token);
            BangumiSearchResults result = ParseSearch(bytes, offset);
            if (_search.Count >= 50) _search.Remove(_search.MinBy(pair => pair.Value.Time).Key);
            _search[key] = (_now(), result);
            return result;
        }
        finally { _queries.Release(); }
    }

    public async Task<BangumiAnime> GetAnimeDetailsAsync(string id, bool fresh, CancellationToken token)
    {
        id = CanonicalId(id);
        await _queries.WaitAsync(token);
        try
        {
            if (!fresh && _details.TryGetValue(id, out var cached) && _now() - cached.Time < Lifetime) return cached.Value;
            byte[] bytes = await transport.GetJsonAsync(new(BangumiNetworkPolicy.ApiRoot, "v0/subjects/" + id), null, token);
            BangumiAnime result = ParseDetails(bytes);
            if (result.Id != id) throw new BangumiRequestException(BangumiFailure.InvalidResponse);
            if (_details.Count >= 100) _details.Remove(_details.MinBy(pair => pair.Value.Time).Key);
            _details[id] = (_now(), result);
            return result;
        }
        finally { _queries.Release(); }
    }

    private static int Integer(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out JsonElement value) || !value.TryGetInt32(out int number))
            throw new BangumiRequestException(BangumiFailure.InvalidResponse);
        return number;
    }

    private static string? Text(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out JsonElement value) || value.ValueKind == JsonValueKind.Null) return null;
        if (value.ValueKind != JsonValueKind.String) throw new BangumiRequestException(BangumiFailure.InvalidResponse);
        string? text = value.GetString()?.Trim();
        if (text?.Length > 100_000) throw new BangumiRequestException(BangumiFailure.InvalidResponse);
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static BangumiAnime ParseAnime(JsonElement item)
    {
        if (Integer(item, "type") != 2) throw new BangumiRequestException(BangumiFailure.InvalidResponse);
        int id = Integer(item, "id");
        string? original = Text(item, "name"), chinese = Text(item, "name_cn");
        if (id <= 0 || chinese is null && original is null) throw new BangumiRequestException(BangumiFailure.InvalidResponse);
        DateOnly? date = DateOnly.TryParseExact(Text(item, "date"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly parsed)
            ? parsed : null;
        int? episodes = null;
        if (item.TryGetProperty("eps", out JsonElement eps) && eps.ValueKind != JsonValueKind.Null)
        {
            if (!eps.TryGetInt32(out int count)) throw new BangumiRequestException(BangumiFailure.InvalidResponse);
            if (count > 0) episodes = count; // total_episodes includes non-regular chapters; never substitute it.
        }
        Uri? image = null, thumbnail = null;
        if (item.TryGetProperty("images", out JsonElement images) && images.ValueKind != JsonValueKind.Null)
        {
            if (images.ValueKind != JsonValueKind.Object) throw new BangumiRequestException(BangumiFailure.InvalidResponse);
            image = ImageUri(Text(images, "large") ?? Text(images, "medium"));
            thumbnail = ImageUri(Text(images, "small") ?? Text(images, "grid")) ?? image;
        }
        return new(id.ToString(CultureInfo.InvariantCulture), chinese ?? original!, original,
            Text(item, "summary"), date, episodes, image, thumbnail) { AnimationStudios = ReadStudios(item) };
    }

    private static IReadOnlyList<string> ReadStudios(JsonElement subject)
    {
        if (!subject.TryGetProperty("infobox", out JsonElement box) || box.ValueKind != JsonValueKind.Array) return [];
        var names = new List<string>();
        void Add(JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.String)
                names.AddRange((value.GetString() ?? string.Empty).Split(['、', '；', ';', '\n'], StringSplitOptions.RemoveEmptyEntries));
        }
        foreach (JsonElement field in box.EnumerateArray())
        {
            if (field.ValueKind != JsonValueKind.Object || !field.TryGetProperty("key", out var key) ||
                key.ValueKind != JsonValueKind.String || key.GetString() is not ("动画制作" or "アニメーション制作") ||
                !field.TryGetProperty("value", out var value)) continue;
            if (value.ValueKind == JsonValueKind.String) Add(value);
            else if (value.ValueKind == JsonValueKind.Array)
                foreach (JsonElement part in value.EnumerateArray())
                {
                    if (part.ValueKind == JsonValueKind.Object && part.TryGetProperty("v", out var text)) Add(text);
                    else Add(part);
                }
        }
        try { return AnimationStudioRules.Normalize(names); }
        catch (AnimeValidationException) { throw new BangumiRequestException(BangumiFailure.InvalidResponse); }
    }

    private static Uri? ImageUri(string? value)
    {
        if (value is null || !Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)) return null;
        try { BangumiNetworkPolicy.ValidateUri(uri, true); return uri; }
        catch (BangumiRequestException) { return null; } // Unsafe/missing cover never prevents metadata import.
    }

    public static BangumiAnime ParseDetails(byte[] bytes)
    {
        try { using JsonDocument document = JsonDocument.Parse(bytes, new() { MaxDepth = 32 }); return ParseAnime(document.RootElement); }
        catch (Exception error) when (error is JsonException or InvalidOperationException or FormatException)
        { throw new BangumiRequestException(BangumiFailure.InvalidResponse); }
    }

    public static BangumiSearchResults ParseSearch(byte[] bytes, int offset)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(bytes, new() { MaxDepth = 32 });
            JsonElement root = document.RootElement;
            int total = Integer(root, "total");
            if (total < 0 || !root.TryGetProperty("data", out JsonElement data) || data.ValueKind != JsonValueKind.Array || data.GetArrayLength() > PageSize)
                throw new BangumiRequestException(BangumiFailure.InvalidResponse);
            var items = new List<BangumiAnime>();
            foreach (JsonElement item in data.EnumerateArray())
                if (Integer(item, "type") == 2) items.Add(ParseAnime(item));
            return new(items.DistinctBy(item => item.Id).ToArray(), total, offset);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or FormatException)
        { throw new BangumiRequestException(BangumiFailure.InvalidResponse); }
    }
}
