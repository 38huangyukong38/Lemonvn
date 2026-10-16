namespace GalgameManager.Helpers.API.Bgm;

public class PersonDto
{
    public required int id { get; set; }
    public required string name { get; set; } = string.Empty;
    public required PersonTypeDto type { get; set; }
    public required List<PersonCareerDto> career { get; set; } = [];
    public ImgDto? images { get; set; }
    public required string short_summary { get; set; } = string.Empty;
}

public class PersonDetailDto : PersonDto
{
    public required string summary { get; set; }
    public string? gender { get; set; }
    public int? birth_year { get; set; }
    public int? birth_mon { get; set; }
    public int? birth_day { get; set; }
    public List<InfoBoxItemDto> infobox { get; set; } = [];
}