namespace GalgameManager.WinApp.Base.Models.Plugin;

public enum SidebarButtonPlacement
{
    Menu = 0,
    Footer = 1,
}

public class SidebarButtonInfo
{
    public required string Id { get; set; }
    public required string Text { get; set; }
    public SidebarButtonPlacement Placement { get; set; } = SidebarButtonPlacement.Menu;
    public string? FallbackGlyph { get; set; }
    public string? FluentGlyph { get; set; }
}
