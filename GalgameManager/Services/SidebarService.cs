using System.Runtime.InteropServices;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.WinApp.Base.Models;
using GalgameManager.WinApp.Base.Models.Plugin;

namespace GalgameManager.Services;

public class SidebarService : ISidebarService
{
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IInfoService _infoService;
    private readonly object _lock = new();
    private readonly Dictionary<string, bool> _visibilityMap;

    private readonly List<SidebarButtonDefinition> _builtInButtons =
    [
        new()
        {
            UniqueId = SidebarButtonIds.MultiStream,
            Title = "MultiStreamText".GetLocalized(),
            Placement = SidebarButtonPlacement.Menu,
            Order = 0,
            FallbackGlyph = "&#xE80F;",
            FluentGlyph = "&#xE80F;",
        },
        new()
        {
            UniqueId = SidebarButtonIds.Home,
            Title = "Home_Text".GetLocalized(),
            Placement = SidebarButtonPlacement.Menu,
            Order = 1,
            FallbackGlyph = "&#xF0E2;",
            FluentGlyph = "&#xF0E2;",
        },
        new()
        {
            UniqueId = SidebarButtonIds.Library,
            Title = "LibraryText".GetLocalized(),
            Placement = SidebarButtonPlacement.Menu,
            Order = 2,
            FallbackGlyph = "&#xE8B7;",
            FluentGlyph = "&#xE8B7;",
        },
        new()
        {
            UniqueId = SidebarButtonIds.Category,
            Title = "CategoryText".GetLocalized(),
            Placement = SidebarButtonPlacement.Menu,
            Order = 3,
            FallbackGlyph = "&#xE7C1;",
            FluentGlyph = "&#xE7C1;",
        },
        new()
        {
            UniqueId = SidebarButtonIds.AnnualReport,
            Title = "AnnualReportText".GetLocalized(),
            Placement = SidebarButtonPlacement.Footer,
            Order = 0,
            FallbackGlyph = "&#xE734;",
            FluentGlyph = "&#xE734;",
        },
        new()
        {
            UniqueId = SidebarButtonIds.Info,
            Title = "InfoText".GetLocalized(),
            Placement = SidebarButtonPlacement.Footer,
            Order = 1,
            FallbackGlyph = "&#xE770;",
            FluentGlyph = "&#xE770;",
        },
        new()
        {
            UniqueId = SidebarButtonIds.Help,
            Title = "HelpText".GetLocalized(),
            Placement = SidebarButtonPlacement.Footer,
            Order = 2,
            FallbackGlyph = "&#xE897;",
            FluentGlyph = "&#xE897;",
        },
        new()
        {
            UniqueId = SidebarButtonIds.Account,
            Title = "AccountText".GetLocalized(),
            Placement = SidebarButtonPlacement.Footer,
            Order = 3,
            FallbackGlyph = "&#xE77B;",
            FluentGlyph = "&#xE77B;",
        },
        new()
        {
            UniqueId = SidebarButtonIds.Plugin,
            Title = "Plugin".GetLocalized(),
            Placement = SidebarButtonPlacement.Footer,
            Order = 4,
            FallbackGlyph = "\uE710",
            FluentGlyph = "&#xE74C;",
        },
        new()
        {
            UniqueId = SidebarButtonIds.Settings,
            Title = "SettingsText".GetLocalized(),
            Placement = SidebarButtonPlacement.Footer,
            Order = 100,
            FallbackGlyph = "&#xE713;",
            FluentGlyph = "&#xE713;",
        },
    ];

    private readonly Dictionary<string, SidebarButtonDefinition> _pluginButtons = [];

    public event Action? ButtonsChanged;

    public SidebarService(ILocalSettingsService localSettingsService, IInfoService infoService)
    {
        _localSettingsService = localSettingsService;
        _infoService = infoService;
        _visibilityMap = _localSettingsService
            .ReadSettingAsync<Dictionary<string, bool>>(KeyValues.SidebarButtonVisibility).Result ?? [];
        _localSettingsService.OnSettingChanged += OnLocalSettingChanged;
    }

    public IReadOnlyList<SidebarButton> GetButtons()
    {
        lock (_lock)
        {
            return _builtInButtons.Concat(_pluginButtons.Values)
                .Select(ToButton)
                .OrderBy(b => b.Placement)
                .ThenBy(b => b.Order)
                .ThenBy(b => b.Title, StringComparer.CurrentCulture)
                .ToList();
        }
    }

    public async Task InitAsync()
    {
        if (!await _localSettingsService.ReadSettingAsync<bool>(KeyValues.HideYearReport2026))
        {
            try
            {
                await SetVisibilityAsync(SidebarButtonIds.AnnualReport, false);
                await _localSettingsService.SaveSettingAsync(KeyValues.HideYearReport2026, true);
            }
            catch (Exception)
            {
                //ignore
            }
        }
    }

    public async Task SaveVisibilityAsync(IReadOnlyDictionary<string, bool> visibility)
    {
        Dictionary<string, bool> map;
        lock (_lock)
        {
            map = new Dictionary<string, bool>(_visibilityMap);
            foreach (KeyValuePair<string, bool> pair in visibility)
                _visibilityMap[pair.Key] = pair.Value;
            foreach (KeyValuePair<string, bool> pair in visibility)
                map[pair.Key] = pair.Value;
        }

        await _localSettingsService.SaveSettingAsync(KeyValues.SidebarButtonVisibility, map);
        RaiseButtonsChanged();
    }

    public async Task SetVisibilityAsync(string uniqueId, bool isVisible)
    {
        if (uniqueId == SidebarButtonIds.Settings) return;
        if (_builtInButtons.All(button => button.UniqueId != uniqueId) && !_pluginButtons.ContainsKey(uniqueId))
            throw new PvnException($"No button with uniqueId '{uniqueId}' exists.");
        _visibilityMap[uniqueId] = isVisible;
        await SaveVisibilityAsync(_visibilityMap);
    }

    public void RegisterPluginButton(Guid pluginId, string pluginName, SidebarButtonInfo button, Func<Task> onClick)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(button.Id);
        ArgumentNullException.ThrowIfNull(button);
        ArgumentNullException.ThrowIfNull(onClick);

        SidebarButtonDefinition definition = new()
        {
            UniqueId = SidebarButtonIds.CreatePluginButtonId(pluginId, button.Id),
            Title = button.Text,
            Description = pluginName,
            Placement = button.Placement,
            Order = GetNextPluginOrder(button.Placement),
            FallbackGlyph = button.FallbackGlyph,
            FluentGlyph = button.FluentGlyph,
            IsPlugin = true,
            Callback = onClick,
            PluginId = pluginId,
            PluginInfo = new PluginInfo
            {
                Id = pluginId,
                Name = pluginName,
                Description = pluginName,
            },
        };

        lock (_lock)
        {
            if (_pluginButtons.TryGetValue(definition.UniqueId, out SidebarButtonDefinition? existing))
                if (existing.Placement == definition.Placement)
                    definition.Order = existing.Order;
            _pluginButtons[definition.UniqueId] = definition;
        }

        RaiseButtonsChanged();
    }

    public void UnregisterPluginButton(Guid pluginId, string buttonId)
    {
        var uniqueId = SidebarButtonIds.CreatePluginButtonId(pluginId, buttonId);
        bool changed;
        lock (_lock) changed = _pluginButtons.Remove(uniqueId);
        if (changed) RaiseButtonsChanged();
    }

    public void UnregisterAllPluginButtons(Guid pluginId)
    {
        var changed = false;
        lock (_lock)
        {
            List<string> keys = _pluginButtons
                .Where(pair => pair.Value.PluginId == pluginId)
                .Select(pair => pair.Key)
                .ToList();
            foreach (var key in keys) changed |= _pluginButtons.Remove(key);
        }
        if (changed) RaiseButtonsChanged();
    }

    public bool IsPluginButton(string uniqueId)
    {
        lock (_lock) return _pluginButtons.ContainsKey(uniqueId);
    }

    public async Task InvokeButtonAsync(string uniqueId)
    {
        SidebarButtonDefinition? definition;
        lock (_lock) definition = _pluginButtons.GetValueOrDefault(uniqueId);
        if (definition?.Callback is null) return;
        if (definition.PluginInfo is not null)
            await PluginInvokeHelper.InvokeAsync(definition.PluginInfo, definition.Callback, _infoService);
        else
            await definition.Callback();
    }

    private SidebarButton ToButton(SidebarButtonDefinition definition)
    {
        return new SidebarButton
        {
            UniqueId = definition.UniqueId,
            Title = definition.Title,
            Description = definition.Description,
            Placement = definition.Placement,
            IsVisible = definition.UniqueId == SidebarButtonIds.Settings ||
                        !_visibilityMap.TryGetValue(definition.UniqueId, out var visible) || visible,
            IsPlugin = definition.IsPlugin,
            FallbackGlyph = definition.FallbackGlyph,
            FluentGlyph = definition.FluentGlyph,
            Order = definition.Order,
        };
    }

    private int GetNextPluginOrder(SidebarButtonPlacement placement)
    {
        lock (_lock)
        {
            int builtInMax = _builtInButtons
                .Where(button => button.Placement == placement)
                .Where(button => placement != SidebarButtonPlacement.Footer || button.UniqueId != SidebarButtonIds.Settings)
                .Select(button => button.Order)
                .DefaultIfEmpty(-1)
                .Max();
            int pluginMax = _pluginButtons.Values
                .Where(button => button.Placement == placement)
                .Select(button => button.Order)
                .DefaultIfEmpty(builtInMax)
                .Max();
            return Math.Max(builtInMax, pluginMax) + 1;
        }
    }

    private void OnLocalSettingChanged(string key, object? value)
    {
        if (key != KeyValues.SidebarButtonVisibility)
            return;

        lock (_lock)
        {
            _visibilityMap.Clear();
            if (value is Dictionary<string, bool> visibility)
            {
                foreach (KeyValuePair<string, bool> pair in visibility)
                    _visibilityMap[pair.Key] = pair.Value;
            }
        }

        RaiseButtonsChanged();
    }

    private void RaiseButtonsChanged()
    {
        try
        {
            UiThreadInvokeHelper.Invoke(() => ButtonsChanged?.Invoke());
        }
        catch (Exception ex) when (ex is COMException or TypeInitializationException)
        {
            ButtonsChanged?.Invoke();
        }
    }

    private sealed class SidebarButtonDefinition
    {
        public required string UniqueId { get; init; }
        public required string Title { get; init; }
        public string? Description { get; init; }
        public SidebarButtonPlacement Placement { get; init; }
        public int Order { get; set; }
        public string? FallbackGlyph { get; init; }
        public string? FluentGlyph { get; init; }
        public bool IsPlugin { get; init; }
        public Func<Task>? Callback { get; init; }
        public Guid? PluginId { get; init; }
        public PluginInfo? PluginInfo { get; init; }
    }
}
