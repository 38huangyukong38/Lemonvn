using GalgameManager.Contracts.Services;
using GalgameManager.Models.Sources;
using Newtonsoft.Json;

namespace GalgameManager.Helpers;

public class GalgameSourceAndUrlConverter(IGalgameSourceCollectionService sourceService)
    : JsonConverter<GalgameSourceBase>
{
    public override void WriteJson(JsonWriter writer, GalgameSourceBase? value, JsonSerializer serializer)
    {
        serializer.Serialize(writer, value?.Url);
    }

    public override GalgameSourceBase? ReadJson(JsonReader reader, Type objectType, GalgameSourceBase? existingValue,
        bool hasExistingValue,
        JsonSerializer serializer)
    {
        var url = serializer.Deserialize<string?>(reader);
        if (url is null) return null;
        return sourceService.GetGalgameSourceFromUrl(url);
    }
}