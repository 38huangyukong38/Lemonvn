using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using Microsoft.UI.Xaml;

namespace GalgameManager.Views;

public static class UiDefaultValues
{
    private static readonly ILocalSettingsService SettingsService = App.GetService<ILocalSettingsService>();
    private static readonly IInfoService InfoService = App.GetService<IInfoService>();
    private static bool _init;
    public static Visibility GamePrefabDisplayPlayType { get; private set; }
    public static Visibility GamePrefabDisplayName { get; private set; }

    public static void Init()
    {
        if (_init) return;
        SettingsService.OnSettingChanged += GetValues;
        GetValues(string.Empty, null);
        _init = true;
    }

    private static async void GetValues(string key, object? value)
    {
        try
        {
            GamePrefabDisplayPlayType =
                (await SettingsService.ReadSettingAsync<bool>(KeyValues.DisplayPlayTypePolygon)).ToVisibility();
            GamePrefabDisplayName = 
                (await SettingsService.ReadSettingAsync<bool>(KeyValues.ShowGameNameInControl)).ToVisibility();
        }
        catch (Exception e)
        {
            InfoService.DeveloperEvent(e: e);
        }
    }
}