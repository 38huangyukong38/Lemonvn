namespace LemonVN.Contracts.Services;

public interface IAnimeUiService
{
    Task<string?> PickCoverAsync();
    Task<bool> ConfirmDeleteAsync(string title);
    Task<bool> ConfirmMarkThroughAsync(int episodeNumber);
}
