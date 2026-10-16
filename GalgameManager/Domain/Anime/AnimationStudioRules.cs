namespace LemonVN.Domain.Anime;

public static class AnimationStudioRules
{
    public const int MaxStudios = 32;
    public const int MaxLength = 128;

    public static List<string> Parse(string text) => Normalize(text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries));

    public static List<string> Normalize(IEnumerable<string> names)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string? name in names)
        {
            if (name is null) throw new AnimeValidationException("Animation studio names must be text.");
            string value = name.Trim();
            if (value.Length == 0) continue;
            if (value.Length > MaxLength || value.Any(char.IsControl))
                throw new AnimeValidationException("Invalid or excessive animation studio name.");
            if (seen.Add(value)) result.Add(value);
            if (result.Count > MaxStudios) throw new AnimeValidationException("Too many animation studios.");
        }
        return result;
    }

    public static void Validate(List<string>? names)
    {
        if (names is null || !names.SequenceEqual(Normalize(names), StringComparer.Ordinal))
            throw new AnimeValidationException("Animation studios must be a canonical text list.");
    }
}
