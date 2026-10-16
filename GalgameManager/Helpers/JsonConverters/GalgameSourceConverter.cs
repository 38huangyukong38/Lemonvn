using GalgameManager.Models;
using GalgameManager.Models.Sources;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GalgameManager.Helpers;

public class GalgameSourceConverter : JsonConverter<GalgameSourceBase>
{
    // 我们不自定义写入过程，让默认序列化器处理。这样可以避免无限递归，并且能正确序列化所有派生类属性
    public override bool CanWrite => false;

    // 因为 CanWrite 是 false，这个方法永远不会被调用
    public override void WriteJson(JsonWriter writer, GalgameSourceBase? value, JsonSerializer serializer)
    {
        // nothing to do here
    }

    public override GalgameSourceBase? ReadJson(JsonReader reader, Type objectType, GalgameSourceBase? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
        {
            return null;
        }

        JObject jObject = JObject.Load(reader);
        if (!jObject.TryGetValue(nameof(GalgameSourceBase.SourceType), StringComparison.OrdinalIgnoreCase, out JToken? typeToken))
            throw new JsonSerializationException(
                $"Cannot create GalgameSourceBase object. Missing '{nameof(GalgameSourceBase.SourceType)}' property.");

        GalgameSourceType sourceType = typeToken.ToObject<GalgameSourceType>();
        GalgameSourceBase target = sourceType switch
        {
            GalgameSourceType.LocalFolder => new GalgameFolderSource(),
            GalgameSourceType.LocalZip    => new GalgameZipSource(),
            GalgameSourceType.Virtual     => new VirtualSource(),
            GalgameSourceType.Steam       => new SteamSource(),
            _  => throw new PvnException("Please implement GalgameSourceConverter for new source types."),
        };
        using JsonReader subReader = jObject.CreateReader();
        serializer.Populate(subReader, target);
        return target;
    }
}
