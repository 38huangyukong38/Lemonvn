using GalgameManager.Contracts.Services;
using GalgameManager.Models;
using Newtonsoft.Json;

namespace GalgameManager.Helpers;

public class CategoryGroupAndUuidConverter (ICategoryService categoryService) : JsonConverter<CategoryGroup>
{
    public override void WriteJson(JsonWriter writer, CategoryGroup? value, JsonSerializer serializer)
    {
        serializer.Serialize(writer, value?.Id);
    }

    public override CategoryGroup? ReadJson(JsonReader reader, Type objectType, CategoryGroup? existingValue,
        bool hasExistingValue,
        JsonSerializer serializer)
    {
        Guid? uid = serializer.Deserialize<Guid?>(reader);
        return uid is null ? null : categoryService.GetGroup(uid.Value);
    }
}
