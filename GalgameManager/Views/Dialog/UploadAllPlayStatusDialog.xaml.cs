using GalgameManager.Contracts.Services;
using GalgameManager.Models;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Services;
using Microsoft.UI.Xaml;

namespace GalgameManager.Views.Dialog;

public sealed partial class UploadAllPlayStatusDialog
{
    /// <summary>
    /// 是否被取消了
    /// </summary>
    public bool Canceled;

    /// <summary>
    /// 是否选择上传到Bangumi
    /// </summary>
    public bool SelectedBangumi { get; private set; }

    /// <summary>
    /// 是否选择上传到VNDB
    /// </summary>
    public bool SelectedVndb { get; private set; }

    public UploadAllPlayStatusDialog()
    {
        InitializeComponent();
        RequestedTheme = App.MainWindow?.Content is FrameworkElement element ? element.RequestedTheme : RequestedTheme;
        XamlRoot = App.MainWindow!.Content!.XamlRoot;
        PrimaryButtonText = "Yes".GetLocalized();
        SecondaryButtonText = "Cancel".GetLocalized();
        Title = "UploadAllPlayStatusDialog_Title".GetLocalized();

        ILocalSettingsService settings = App.GetService<ILocalSettingsService>();
        BgmAccount? bgmState = settings.ReadSettingAsync<BgmAccount>(KeyValues.BangumiAccount).Result;
        VndbAccount? vndbAccount = settings.ReadSettingAsync<VndbAccount>(KeyValues.VndbAccount).Result;

        var hasBangumi = bgmState is not null;
        var hasVndb = vndbAccount is not null;

        BangumiCheckBox.IsEnabled = hasBangumi;
        VndbCheckBox.IsEnabled = hasVndb;
        BangumiCheckBox.IsChecked = hasBangumi;
        VndbCheckBox.IsChecked = hasVndb;

        PrimaryButtonClick += (_, _) =>
        {
            SelectedBangumi = BangumiCheckBox.IsChecked == true;
            SelectedVndb = VndbCheckBox.IsChecked == true;
        };

        SecondaryButtonClick += (_, _) => Canceled = true;
    }
}