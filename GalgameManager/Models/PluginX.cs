using CommunityToolkit.Mvvm.ComponentModel;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Services;
using GalgameManager.WinApp.Base.Contracts;
using GalgameManager.WinApp.Base.Models;
using LiteDB;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Models;

public partial class PluginX : ObservableObject, IComparable<PluginX>
{
    [BsonId] public Guid Id { get; init; }
    [BsonIgnore] public IPlugin? Plugin { get; set; }
    public PluginInfo Info { get; set; }
    public Version Version { get; set; } //插件版本，只有商店下载的插件才有效
    [ObservableProperty] private string _path;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Logo))] private string? _logoUrl;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Logo))] private string? _logoPath;
    public string? Logo => !string.IsNullOrEmpty(LogoPath) ? LogoPath : LogoUrl;
    [ObservableProperty] private DateTime _releaseDate;

    /// 是否启用，注意，设置为false只会让下次启动不加载该插件，不会在当前卸载
    [ObservableProperty] private bool _enable = false;
    /// 如果设置为true，该插件会在下次启动时被删除
    public bool ToDelete { get; set; }
    /// 删除插件时是否连同数据一起删除
    public bool ToDeleteData { get; set; }
    /// 是否已经加载
    [BsonIgnore] public bool IsLoaded { get; set; } = false;
    [BsonIgnore] public PluginLoadContext LoadContext { get; set; }
    [BsonIgnore] public IDisposable? XamlRegistration { get; set; }

    // 插件是否是在 Dev 模式下加载的
    [ObservableProperty] private bool _isDevMode = false;

    [Obsolete("For deserialization only", true)]
    public PluginX()
    {
        Plugin = null!;
        Info = null!;
        _path = string.Empty;
        LoadContext = null!;
        Version = null!;
    }

    /// <inheritdoc/>
    public PluginX(IPlugin plugin, string path, PluginLoadContext context)
    {
        Plugin = plugin;
        _path = path;
        LoadContext = context;
        Info = plugin.Info.ShallowClone();
        Id = Info.Id;
        Version = new();
    }

    /// <summary>
    /// 对获取插件UI的函数的包装，对于执行时间过长的函数（这会卡住主进程）进行提示
    /// </summary>
    /// <param name="func"></param>
    /// <returns></returns>
    public UIElement? GetPluginUi(Func<UIElement?> func)
    {
        DateTime start = DateTime.Now;
        UIElement? ui = func();
        DateTime end = DateTime.Now;
        TimeSpan span = end - start;
        if (span.TotalMilliseconds > 1000)
            App.GetService<IInfoService>().Event(EventType.PluginError, InfoBarSeverity.Informational,
                "PluginX_UiSlow_Title".GetLocalized(), msg: "PluginX_UiSlow_Msg".GetLocalized(Info.Name));
        return ui;
    }

    /// <summary>
    /// 安全地调用插件的 OnUninstall 方法，支持最大执行时间限制及动态延时请求。
    /// </summary>
    /// <remarks>
    /// 此方法会启动一个受监控的卸载任务：
    /// <list type="bullet">
    /// <item>默认等待时间为 5 秒。</item>
    /// <item>插件可通过回调请求延长等待时间，但总时长不会超过 60 秒。</item>
    /// <item>一旦超时，将自动取消任务并抛出 <see cref="TimeoutException"/>。</item>
    /// </list>
    /// </remarks>
    public async Task ExecuteUninstallWithTimeoutAsync()
    {
        // Only wait for 5 seconds for unload to complete.
        TimeSpan initialTimeout = TimeSpan.FromSeconds(5);
        TimeSpan maxTimeout = TimeSpan.FromSeconds(60);

        using var cts = new CancellationTokenSource();

        var startTime = DateTime.UtcNow;
        var deadline = startTime.Add(initialTimeout);
        var hardDeadline = startTime.Add(maxTimeout);
        cts.CancelAfter(hardDeadline - startTime);

        Action<TimeSpan> extendWaitHandler = (extraTime) =>
        {
            DateTime newDeadline = DateTime.UtcNow.Add(extraTime);
            if (newDeadline > hardDeadline) newDeadline = hardDeadline;
            if (newDeadline > deadline) deadline = newDeadline;
        };

        if (Plugin is not null)
        {
            Task uninstallTask = Plugin.OnUninstallAsync(ToDeleteData, extendWaitHandler, cts.Token);
            while (!uninstallTask.IsCompleted)
            {
                var now = DateTime.UtcNow;
                if (now >= deadline)
                {
                    await cts.CancelAsync();
                    throw new TimeoutException("Plugin Uninstall Timeout");
                }

                var waitTime = deadline - now;
                var delayTask = Task.Delay(waitTime, cts.Token);
                var completeTask = await Task.WhenAny(uninstallTask, delayTask);

                if (completeTask == uninstallTask)
                    break;
            }
            await uninstallTask;
        }
    }

    public void ForceUnload()
    {
        IsLoaded = false;
        Info = null!;
        XamlRegistration?.Dispose();
        XamlRegistration = null;
        Plugin = null;
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (LoadContext is not null) LoadContext.Unload();
        LoadContext = null!;
    }

    public int CompareTo(PluginX? other) {
        if (other is null) return 0;
        // 优先级1: IsDevMode 降序 (true 在前)
        var devCompare = other.IsDevMode.CompareTo(IsDevMode);
        if (devCompare != 0) return devCompare;
        // 优先级2: Name 升序
        return string.Compare(Info.Name, other.Info.Name, StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj)
    {
        if (obj is PluginX other)
        {
            return Id.Equals(other.Id);
        }
        return false;
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
}

public static class PluginExtensions
{
    public static PluginInfo ShallowClone(this PluginInfo info) => new()
    {
        Description = info.Description,
        Id = info.Id,
        Name = info.Name
    };
}
