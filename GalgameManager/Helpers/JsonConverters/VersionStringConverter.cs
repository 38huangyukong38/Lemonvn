using Newtonsoft.Json;

namespace GalgameManager.Helpers;

public class VersionStringConverter : JsonConverter<Version>
{
    public override void WriteJson(JsonWriter writer, Version? value, JsonSerializer serializer)
    {
        // 序列化时转换为字符串
        writer.WriteValue(value?.ToString());
    }

    public override Version ReadJson(JsonReader reader, Type objectType, Version? existingValue, bool hasExistingValue,
        JsonSerializer serializer)
    {
        if (reader.TokenType != JsonToken.String || reader.Value is not string str)
            return new Version();
        return Version.Parse(str);
    }
}