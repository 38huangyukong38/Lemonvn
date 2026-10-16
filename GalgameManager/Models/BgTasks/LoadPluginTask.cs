using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Services;
using LiteDB;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Models.BgTasks;

public class LoadPluginTask : BgTaskBase
{
    private readonly IPluginService _pluginService = App.GetService<IPluginService>();
    private readonly ILocalSettingsService _settingService = App.GetService<ILocalSettingsService>();
    private readonly IInfoService _infoService = App.GetService<IInfoService>();
    private DirectoryInfo _pluginDir = null!;

    protected override Task RecoverFromJsonInternal() => Task.CompletedTask;

    protected override Task RunInternal() => Task.Run(async () =>
    {
        _pluginDir = _pluginService.PluginDir;
        ILiteCollection<PluginX> db = _settingService.Database.GetCollection<PluginX>("plugin");
        ILiteCollection<PluginService.PluginData> dataDb = _settingService.Database.GetCollection<PluginService.PluginData>("plugin_data");
        List<string> pluginPaths = await _settingService.ReadSettingAsync<List<string>>(KeyValues.PluginPaths) ?? [];
        List<PluginX> plugins = db.FindAll().ToList();
        for (var i = 0; i < plugins.Count; i++)
        {
            PluginX plugin = plugins[i];
            if (plugin.ToDelete)
            {
                await DeletePlugin(plugin);
                continue;
            }

            ChangeProgress(i, pluginPaths.Count, "LoadPluginTask_Loading".GetLocalized(plugin.Info.Name));
            try
            {
                await _pluginService.LoadPluginAsync(plugin, plugin.Enable);
            }
            catch (Exception e)
            {
                await _pluginService.LoadFailedPluginAsync(plugin);
                var msg = "LoadPluginTask_ErrorLoading_Msg".GetLocalized(e is PvnException ex ? ex.FullMsg : e.ToString());
                _infoService.Event(EventType.PluginError, InfoBarSeverity.Warning,
                    "LoadPluginTask_ErrorLoading".GetLocalized(plugin.Info.Name), msg: msg);
            }
        }
        ChangeProgress(1, 1, string.Empty, false);
        return;

        async Task DeletePlugin(PluginX plugin)
        {
            try
            {
                App.GetService<ISidebarService>().UnregisterAllPluginButtons(plugin.Id);
                // 处理所有延迟删除的 Plugin，注意 dev plugin 不存在延迟删除机制。
                if (plugin.IsDevMode)
                {
                    _infoService.Event(EventType.PluginError, InfoBarSeverity.Warning,
                        "Dev Plugin Invalid State", // 标题明确指出是 Dev 插件状态异常
                        msg: $"Dev plugin {plugin.Info.Name} found in delayed delete queue. Cleaning up...");
                }
                else
                {
                    if (Directory.Exists(plugin.Path))
                        Directory.Delete(plugin.Path, true);
                }

                // 如果出现文件占用问题，则不删除 plugin，等待下次重启再删除。
                // 如果 plugin 自身的 OnUninstall 超时或者报错，则正常删除这个插件。
                try { await plugin.ExecuteUninstallWithTimeoutAsync(); } catch(Exception) { /* Ignore */ }
                if (plugin.ToDeleteData) _pluginService.PluginDeleteData(plugin);
                db.Delete(plugin.Id);
            }
            catch (Exception)
            {
                _infoService.Event(EventType.PluginError, InfoBarSeverity.Warning,
                    "LoadPluginTask_ErrorLoading".GetLocalized(plugin.Info.Name),
                    msg: "LoadPluginTask_ErrorDeleting_Msg".GetLocalized(plugin.Path));
            }
        }
    });

    public override string Title => "LoadPluginTask_Title".GetLocalized();
}
