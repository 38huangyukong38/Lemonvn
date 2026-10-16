using GalgameManager.Contracts.Phrase;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;

namespace GalgameManager.Helpers.EnumHelpers;

public static class RssHelperX
{
    public static List<RssType> GetAvailableTypes(IGalgameCollectionService gameService)
    {
        Dictionary<int, IGalInfoPhraser> parsers = gameService.PhraserList;
        return parsers.Keys.Select(k => (RssType)k).ToList();
    }
}