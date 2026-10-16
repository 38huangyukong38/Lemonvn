namespace GalgameManager.Helpers.API.Bgm;

public class SearchPersonPayload
{
    public required string keyword { get; set; }
    public List<string>? filter { get; set; }
}