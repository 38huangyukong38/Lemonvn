using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using LemonVN.Contracts.Services;

namespace LemonVN.Infrastructure.Recognition;

internal static class RecognitionJson
{
    public static string? Text(JsonElement item, string key)
    {
        if (!item.TryGetProperty(key, out var value) || value.ValueKind == JsonValueKind.Null) return null;
        if (value.ValueKind != JsonValueKind.String) throw new RecognitionException(RecognitionFailure.InvalidResponse);
        string text = value.GetString()!;
        if (text.Length > 512 || text.Any(c => char.IsControl(c) && !char.IsWhiteSpace(c)))
            throw new RecognitionException(RecognitionFailure.InvalidResponse);
        text = string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return text.Length == 0 ? null : text;
    }
    public static JsonElement Array(JsonElement root, string key, int maximum)
    {
        if (!root.TryGetProperty(key, out var items) || items.ValueKind != JsonValueKind.Array || items.GetArrayLength() > maximum)
            throw new RecognitionException(RecognitionFailure.InvalidResponse);
        return items;
    }
    public static double? Number(JsonElement root, string key, double maximum)
    {
        if (!root.TryGetProperty(key, out var value) || value.ValueKind == JsonValueKind.Null) return null;
        if (!value.TryGetDouble(out double number) || !double.IsFinite(number) || number < 0 || number > maximum)
            throw new RecognitionException(RecognitionFailure.InvalidResponse);
        return number;
    }
    public static bool? Boolean(JsonElement root, string key)
    {
        if (!root.TryGetProperty(key, out var value) || value.ValueKind == JsonValueKind.Null) return null;
        if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw new RecognitionException(RecognitionFailure.InvalidResponse);
        return value.GetBoolean();
    }
    public static T Parse<T>(byte[] bytes, Func<JsonElement, T> parse)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(bytes, new() { MaxDepth = 32 });
            if (document.RootElement.ValueKind != JsonValueKind.Object) throw new RecognitionException(RecognitionFailure.InvalidResponse);
            return parse(document.RootElement);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or FormatException or OverflowException or KeyNotFoundException)
        { throw new RecognitionException(RecognitionFailure.InvalidResponse); }
    }
}

public sealed class TraceMoeProvider(RecognitionHttpTransport transport)
{
    public Task<RecognitionQuota> GetQuotaAsync(CancellationToken token) => ReadQuotaAsync(token);
    private async Task<RecognitionQuota> ReadQuotaAsync(CancellationToken token) => ParseQuota(
        await transport.SendAsync(RecognitionHttpTransport.TraceQuota, null, false, token));

    public async Task<RecognitionResult> RecognizeAsync(RecognitionImage image, CancellationToken token)
    {
        RecognitionQuota before = await GetQuotaAsync(token);
        if (before.Remaining == 0) throw new RecognitionException(RecognitionFailure.Quota);
        if (before.Concurrency < 1) throw new RecognitionException(RecognitionFailure.Busy);
        byte[] bytes = await transport.SendAsync(RecognitionHttpTransport.TraceSearch, () =>
        {
            var content = new ByteArrayContent(image.CopyBytes());
            content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            return content;
        }, false, token);
        RecognitionResult result = Parse(bytes);
        return result.Quota is null ? result with { Quota = before, QuotaBeforeRequest = true } : result;
    }

    public static RecognitionQuota ParseQuota(byte[] bytes) => RecognitionJson.Parse(bytes, root =>
    {
        int total = root.GetProperty("quota").GetInt32();
        int used = root.TryGetProperty("quotaUsed", out var value) ? value.GetInt32()
            : total - root.GetProperty("quota_remaining").GetInt32();
        int concurrency = root.GetProperty("concurrency").GetInt32();
        if (total < 0 || used < 0 || concurrency < 0 || concurrency > 100)
            throw new RecognitionException(RecognitionFailure.InvalidResponse);
        return new RecognitionQuota(total, used, concurrency);
    });

    public static RecognitionResult Parse(byte[] bytes) => RecognitionJson.Parse(bytes, root =>
    {
        if (RecognitionJson.Text(root, "error") is not null) throw new RecognitionException(RecognitionFailure.InvalidResponse);
        var candidates = new List<RecognitionCandidate>();
        foreach (JsonElement row in RecognitionJson.Array(root, "result", 50).EnumerateArray())
        {
            JsonElement anilist = row.GetProperty("anilist");
            long id;
            var names = new List<string>();
            if (anilist.ValueKind == JsonValueKind.Number) id = anilist.GetInt64();
            else
            {
                id = anilist.GetProperty("id").GetInt64();
                if (anilist.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.Object)
                    foreach (string field in new[] { "native", "romaji", "english" })
                        if (RecognitionJson.Text(title, field) is { } name) names.Add(name);
                if (anilist.TryGetProperty("synonyms", out var aliases) && aliases.ValueKind != JsonValueKind.Null)
                {
                    if (aliases.ValueKind != JsonValueKind.Array || aliases.GetArrayLength() > 32)
                        throw new RecognitionException(RecognitionFailure.InvalidResponse);
                    foreach (JsonElement alias in aliases.EnumerateArray())
                    {
                        if (alias.ValueKind != JsonValueKind.String || alias.GetString()!.Length > 512)
                            throw new RecognitionException(RecognitionFailure.InvalidResponse);
                        string value = alias.GetString()!.Trim();
                        if (value.Any(char.IsControl)) throw new RecognitionException(RecognitionFailure.InvalidResponse);
                        if (value.Length > 0) names.Add(value);
                    }
                }
            }
            if (id <= 0) throw new RecognitionException(RecognitionFailure.InvalidResponse);
            double similarity = RecognitionJson.Number(row, "similarity", 1)
                ?? throw new RecognitionException(RecognitionFailure.InvalidResponse);
            double? from = RecognitionJson.Number(row, "from", 604800), to = RecognitionJson.Number(row, "to", 604800);
            if (from is not null && to < from) throw new RecognitionException(RecognitionFailure.InvalidResponse);
            Uri? preview = null;
            if (RecognitionJson.Text(row, "image") is { } url && Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                try { RecognitionHttpTransport.ValidatePreview(uri); preview = uri; }
                catch (RecognitionException) { /* Unsafe previews never block useful text results. */ }
            }
            string? episode = row.TryGetProperty("episode", out var ep) ? Episode(ep) : null;
            string[] titles = names.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            candidates.Add(new(titles.FirstOrDefault() ?? string.Empty, titles, AniListId: id, Episode: episode,
                FromSeconds: from, ToSeconds: to, Similarity: similarity, Preview: preview));
        }
        RecognitionQuota? quota = null;
        if (root.TryGetProperty("quota", out var q) && root.TryGetProperty("quotaUsed", out var u))
        {
            int total = q.GetInt32(), used = u.GetInt32();
            if (total < 0 || used < 0) throw new RecognitionException(RecognitionFailure.InvalidResponse);
            quota = new(total, used, 1);
        }
        return new RecognitionResult(RecognitionMode.Scene, candidates, quota);
    });

    private static string? Episode(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.Number when value.TryGetDouble(out double number) && double.IsFinite(number) && number >= 0 =>
            number.ToString(CultureInfo.InvariantCulture),
        JsonValueKind.String when value.GetString()!.Length <= 100 && !value.GetString()!.Any(char.IsControl) => value.GetString(),
        JsonValueKind.Array when value.GetArrayLength() <= 16 && value.EnumerateArray().All(item => item.ValueKind != JsonValueKind.Array) =>
            string.Join(", ", value.EnumerateArray().Select(Episode)),
        _ => throw new RecognitionException(RecognitionFailure.InvalidResponse)
    };
}

public sealed class AnimeTraceProvider(RecognitionHttpTransport transport)
{
    public async Task<RecognitionResult> RecognizeAsync(RecognitionImage image, CancellationToken token) => Parse(
        await transport.SendAsync(RecognitionHttpTransport.CharacterSearch, () =>
        {
            var form = new MultipartFormDataContent();
            form.Add(new StringContent("1"), "is_multi");
            form.Add(new StringContent("1"), "ai_detect");
            var file = new ByteArrayContent(image.CopyBytes());
            file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            form.Add(file, "file", "upload.jpg"); // Never send the user's filename or path.
            return form;
        }, false, token));

    public static RecognitionResult Parse(byte[] bytes) => RecognitionJson.Parse(bytes, root =>
    {
        int code = root.GetProperty("code").GetInt32();
        if (code is not (0 or 17720 or 200)) throw new RecognitionException(code switch
        {
            17728 => RecognitionFailure.Quota,
            17702 or 17731 => RecognitionFailure.Busy,
            17704 => RecognitionFailure.Authentication,
            17701 or 17703 or 17705 or 17708 => RecognitionFailure.Image,
            _ => RecognitionFailure.InvalidResponse
        });
        var candidates = new List<RecognitionCandidate>();
        int group = 0;
        foreach (JsonElement face in RecognitionJson.Array(root, "data", 32).EnumerateArray())
        {
            group++;
            RecognitionBox? box = null;
            if (face.TryGetProperty("box", out var region) && region.ValueKind != JsonValueKind.Null)
            {
                if (region.ValueKind != JsonValueKind.Array || region.GetArrayLength() != 4)
                    throw new RecognitionException(RecognitionFailure.InvalidResponse);
                double[] b = region.EnumerateArray().Select(v => v.GetDouble()).ToArray();
                if (b.Any(v => !double.IsFinite(v) || v < 0 || v > 1) || b[0] >= b[2] || b[1] >= b[3])
                    throw new RecognitionException(RecognitionFailure.InvalidResponse);
                box = new(b[0], b[1], b[2], b[3]);
            }
            bool? low = RecognitionJson.Boolean(face, "not_confident");
            foreach (JsonElement item in RecognitionJson.Array(face, "character", 20).EnumerateArray())
            {
                string work = RecognitionJson.Text(item, "work") ?? throw new RecognitionException(RecognitionFailure.InvalidResponse);
                string character = RecognitionJson.Text(item, "character") ?? throw new RecognitionException(RecognitionFailure.InvalidResponse);
                candidates.Add(new(work, [work], character, Group: group, LowConfidence: low, Box: box));
                if (candidates.Count > 100) throw new RecognitionException(RecognitionFailure.InvalidResponse);
            }
        }
        return new RecognitionResult(RecognitionMode.Character, candidates, AiDetected: RecognitionJson.Boolean(root, "ai"));
    });
}
