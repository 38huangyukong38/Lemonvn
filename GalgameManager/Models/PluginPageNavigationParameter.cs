using GalgameManager.WinApp.Base.Models;

namespace GalgameManager.Models;

public class PluginPageNavigationParameter
{
    public required PluginInfo PluginInfo { get; init; }

    public required string PageTypeFullName { get; init; }

    public object? Parameter { get; init; }
}
