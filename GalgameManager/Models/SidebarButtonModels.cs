using CommunityToolkit.Mvvm.ComponentModel;
using GalgameManager.WinApp.Base.Models.Plugin;
namespace GalgameManager.Models;

public static class SidebarButtonIds
{
    public const string MultiStream = "builtin:multi-stream";
    public const string Home = "builtin:home";
    public const string Library = "builtin:library";
    public const string Category = "builtin:category";
    public const string AnnualReport = "builtin:annual-report";
    public const string Info = "builtin:info";
    public const string Help = "builtin:help";
    public const string Account = "builtin:account";
    public const string Plugin = "builtin:plugin";
    public const string Settings = "builtin:settings";

    public static string CreatePluginButtonId(Guid pluginId, string buttonId)
        => $"plugin:{pluginId:N}:{buttonId}";
}

public class SidebarButton
{
    public required string UniqueId { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public SidebarButtonPlacement Placement { get; init; }
    public bool IsVisible { get; init; }
    public bool IsPlugin { get; init; }
    public string? FallbackGlyph { get; init; }
    public string? FluentGlyph { get; init; }
    public int Order { get; init; }
}

public partial class SidebarBtnSettingItem : ObservableObject
{
    public string UniqueId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public SidebarButtonPlacement Placement { get; set; }
    public int Order { get; set; }
    [ObservableProperty] private bool _canToggle;
    [ObservableProperty]  private bool _isVisible;
}
