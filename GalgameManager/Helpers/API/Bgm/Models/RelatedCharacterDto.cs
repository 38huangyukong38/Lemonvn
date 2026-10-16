namespace GalgameManager.Helpers.API.Bgm;

public class RelatedCharacterDto
{
    public required int? id { get; set; }
    public required string name { get; set; }
    public required CharacterTypeDto type { get; set; }
    public ImgDto? images { get; set; }
    public required string relation { get; set; }
    public List<PersonDto>? actors { get; set; }
}