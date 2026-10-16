using GalgameManager.Models;
using GalgameManager.WinApp.Base.Models.Plugin;

namespace GalgameManager.Contracts.Services;

public interface ISidebarService
{
    event Action? ButtonsChanged;

    IReadOnlyList<SidebarButton> GetButtons();

    Task InitAsync();

    Task SaveVisibilityAsync(IReadOnlyDictionary<string, bool> visibility);

    Task SetVisibilityAsync(string uniqueId, bool isVisible);

    void RegisterPluginButton(Guid pluginId, string pluginName, SidebarButtonInfo button, Func<Task> onClick);

    void UnregisterPluginButton(Guid pluginId, string buttonId);

    void UnregisterAllPluginButtons(Guid pluginId);

    bool IsPluginButton(string uniqueId);

    Task InvokeButtonAsync(string uniqueId);
}
