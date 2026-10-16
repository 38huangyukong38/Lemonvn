namespace LemonVN.Contracts.Services;

public sealed record BangumiAnime(string Id, string Title, string? OriginalTitle, string? Description,
    DateOnly? AirDate, int? TotalEpisodes, Uri? CoverUri, Uri? ThumbnailUri)
{
    public IReadOnlyList<string> AnimationStudios { get; init; } = [];
}
public sealed record BangumiSearchResults(IReadOnlyList<BangumiAnime> Items, int Total, int Offset);

public interface IBangumiAnimeProvider
{
    Task<BangumiSearchResults> SearchAnimeAsync(string keyword, int offset, CancellationToken token);
    Task<BangumiAnime> GetAnimeDetailsAsync(string id, bool fresh, CancellationToken token);
}

public interface IRemoteAnimeCoverCache
{
    Task<byte[]?> GetBytesAsync(Uri? uri, CancellationToken token, bool fresh = false);
    Task<string> GetDisplaySourceAsync(Uri? uri, CancellationToken token);
}
