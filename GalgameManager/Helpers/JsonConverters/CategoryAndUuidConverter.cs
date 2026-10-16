using GalgameManager.Contracts.Services;
using GalgameManager.Models;
using Newtonsoft.Json;

namespace GalgameManager.Helpers;

public class CategoryAndUuidConverter(ICategoryService? categoryService = null) : JsonConverter<Category>
{
    public override void WriteJson(JsonWriter writer, Category? value, JsonSerializer serializer)
    {
        serializer.Serialize(writer, value?.Id);
    }

    public override Category? ReadJson(JsonReader reader, Type objectType, Category? existingValue, bool hasExistingValue,
        JsonSerializer serializer)
    {
        Guid? uid = serializer.Deserialize<Guid?>(reader);
        ICategoryService service = categoryService ?? App.GetService<ICategoryService>();
        return uid is null ? null : service.GetCategory(uid.Value);
    }
}
