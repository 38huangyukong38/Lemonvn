namespace GalgameManager.Helpers.API.Bgm;

public class Paged<T>
{
    public int total { get; set; }
    public int limit { get; set; }
    public int offset { get; set; }
    public List<T> data { get; set; } = [];
}