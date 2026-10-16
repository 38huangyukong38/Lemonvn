namespace GalgameManager.Models;

public enum CategoryGroupChangeType
{
    GroupAdded,
    GroupRemoved,
    CategoryAdded,
    CategoryRemoved,
}

public class CategoryGroupChangedArg
{
    public required CategoryGroup Group { get; init; }
    public Category? Category { get; init; }
    public required CategoryGroupChangeType ChangeType { get; init; }
}
