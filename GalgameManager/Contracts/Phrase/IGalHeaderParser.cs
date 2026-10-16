using GalgameManager.Models;

namespace GalgameManager.Contracts.Phrase;

public interface IGalHeaderParser
{
    public Task<string?> GetGalHeaderAsync(Galgame game);
}