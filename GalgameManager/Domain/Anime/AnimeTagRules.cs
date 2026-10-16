namespace LemonVN.Domain.Anime;

public static class AnimeTagRules
{
    public const int MaxLength = 64;
    public const int MaxTags = 50;

    public static List<string> Parse(string text) => Normalize(text.Split([',', '，', ';', '；', '\r', '\n']));

    public static List<string> Normalize(IEnumerable<string> values)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string? value in values)
        {
            string tag = value?.Trim() ?? string.Empty;
            if (tag.Length == 0) continue;
            if (tag.Length > MaxLength || tag.Any(char.IsControl))
                throw new AnimeValidationException($"Tags must have at most {MaxLength} characters and no control characters.");
            if (seen.Add(tag)) result.Add(tag);
        }
        if (result.Count > MaxTags) throw new AnimeValidationException($"An anime can have at most {MaxTags} personal tags.");
        return result;
    }
}
