using Newtonsoft.Json.Linq;

namespace GalgameManager.Helpers.API.Bgm;

public class InfoBoxItemDto
{
    public required string key { get; set; }
    public required JToken value { get; set; }
}

public class InfoBoxItemKVDto
{
    public string k { get; set; } = "";
    public string v { get; set; } = "";
}