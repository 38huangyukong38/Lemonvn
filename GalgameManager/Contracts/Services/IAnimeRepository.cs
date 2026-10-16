using LemonVN.Domain.Anime;

namespace LemonVN.Contracts.Services;

public interface IAnimeRepository
{
    Task<IReadOnlyList<AnimeEntry>> GetAllAsync();
    Task<AnimeEntry?> GetByIdAsync(Guid id);
    Task<AnimeEntry> AddAsync(AnimeEntry entry);
    Task<AnimeEntry> UpdateAsync(AnimeEntry entry);
    Task DeleteAsync(Guid id, long expectedRevision);
    Task<AnimeEntry> RestoreAsync(Guid id);
    Task<AnimeEntry> RestoreAsync(Guid id, long expectedRevision);
    Task<IReadOnlyList<AnimeEntry>> GetTrashAsync();
    Task PermanentlyDeleteAsync(Guid id, long expectedRevision);
    Task<AnimeLibrarySnapshot> GetSnapshotAsync();
    Task ReplaceAllAsync(AnimeLibrarySnapshot snapshot, string expectedSignature, CancellationToken cancellationToken);
}
