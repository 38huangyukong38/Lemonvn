using System.Security.Cryptography;
using System.Text;

namespace LemonVN.Domain.Anime;

public sealed record AnimeLibrarySnapshot(IReadOnlyList<AnimeEntry> Entries, IReadOnlyList<AnimeEntry> Trash)
{
    public string Signature => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n",
        Entries.Select(entry => $"active:{entry.Id:N}:{entry.Revision}:{entry.Metadata.CoverRef}")
            .Concat(Trash.Select(entry => $"trash:{entry.Id:N}:{entry.Revision}:{entry.Metadata.CoverRef}"))
            .Order(StringComparer.Ordinal)))));

    public void Validate()
    {
        foreach (AnimeEntry entry in Entries.Concat(Trash)) AnimeValidation.Validate(entry);
        if (Entries.Concat(Trash).GroupBy(entry => entry.Id).Any(group => group.Count() > 1))
            throw new InvalidDataException("Duplicate anime GUID in library or recycle bin.");
        if (Entries.Where(entry => entry.Metadata.BangumiId is not null)
            .GroupBy(entry => entry.Metadata.BangumiId).Any(group => group.Count() > 1))
            throw new InvalidDataException("Duplicate Bangumi ID in active library.");
    }
}
