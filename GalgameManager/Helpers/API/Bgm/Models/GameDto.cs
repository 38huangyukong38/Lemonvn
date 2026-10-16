namespace GalgameManager.Helpers.API.Bgm;

public class GameDto
{
    public required int id { get; set; } = -1;
    public required string name { get; set; } = string.Empty;
    public required string name_cn { get; set; } = string.Empty;
    public required string summary { get; set; } = string.Empty;
    public string? date { get; set; }
    public required ImgDto images { get; set; } = new();
    public List<InfoBoxItemDto>? infobox { get; set; }
    public required RatingDto rating { get; set; } = new();
    public required TagDto[] tags { get; set; } = [];
}