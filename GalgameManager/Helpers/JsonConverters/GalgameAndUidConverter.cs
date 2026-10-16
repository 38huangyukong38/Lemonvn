using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Models;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json;

namespace GalgameManager.Helpers;

public class GalgameAndUidConverter : JsonConverter<Galgame>
{
    public override void WriteJson(JsonWriter writer, Galgame? value, JsonSerializer serializer)
    {
        // serializer.Serialize(writer, value?.Uid);
        serializer.Serialize(writer, value?.Uuid);
    }

    public override Galgame? ReadJson(JsonReader reader, Type objectType, Galgame? existingValue, bool hasExistingValue,
        JsonSerializer serializer)
    {
        try
        {
            IGalgameCollectionService gameService = App.GetService<IGalgameCollectionService>();
            if (reader.TokenType == JsonToken.StartObject) // 旧版本，GalgameUid
                return gameService.GetGalgameFromUid(serializer.Deserialize<GalgameUid>(reader));
            // 新版本，使用Uuid映射游戏
            return gameService.GetGalgameFromUuid(serializer.Deserialize<Guid>(reader));
        }
        catch (Exception e)
        {
            App.GetService<IInfoService>()
                .Event(EventType.NotCriticalUnexpectedError, InfoBarSeverity.Error, "读取游戏时出现错误", e);
            return null;
        }
    }
}