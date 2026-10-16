namespace GalgameManager.Helpers.API.Bgm;

public class RelatedPersonDto
{
    public required int id { get; set; } = -1;
    public required string name { get; set; } = string.Empty;
    public required PersonTypeDto type { get; set; } = new();
    public required List<PersonCareerDto> career { get; set; } = new();
    public ImgDto? images { get; set; }
    public required string relation { get; set; } = string.Empty;
}