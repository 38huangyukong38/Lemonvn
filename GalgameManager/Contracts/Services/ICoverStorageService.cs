namespace LemonVN.Contracts.Services;

public interface ICoverStorageService
{
    Task<string> ImportAsync(Guid animeId, string sourcePath);
    string GetDisplaySource(string? coverRef);
    void RemoveUncommitted(string coverRef);
    Task<byte[]> ReadOwnedAsync(string coverRef, CancellationToken cancellationToken);
    Task<string> ImportBytesAsync(Guid animeId, byte[] bytes, CancellationToken cancellationToken);
    IReadOnlyList<string> ListOwnedReferences();
}
