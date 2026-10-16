using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GalgameManager.Views.Dialog;

public sealed partial class ManageGalgamePageLayoutDialog : ContentDialog
{
    private readonly ILocalSettingsService _localSettingsService = App.GetService<ILocalSettingsService>();

    public DisplayName[] PrimaryTitleTypes { get; } = { DisplayName.ChineseName, DisplayName.OriginalName, DisplayName.Name };
    public DisplayName[] SecondaryTitleTypes { get; } = { DisplayName.ChineseName, DisplayName.OriginalName, DisplayName.Name, DisplayName.None };
    public DisplayName GalgamePagePrimaryTitleType { get; set; } = DisplayName.ChineseName;
    public DisplayName GalgamePageSecondaryTitleType { get; set; } = DisplayName.OriginalName;

    public bool GalgamePageNewLayout_ShowPainter { get; set; }
    public bool GalgamePageNewLayout_ShowSeiyu { get; set; }
    public bool GalgamePageNewLayout_ShowWriter { get; set; }
    public bool GalgamePageNewLayout_ShowMusician { get; set; }
    public bool GalgamePageNewLayout_ShowBackground { get; set; }
    public bool GalgamePageNewLayout_ShowCover { get; set; }
    public bool GalgamePageNewLayout_ShowCoverWhenNoBackground { get; set; }
    public bool GalgamePageNewLayout_ShowExpectedPlayTime { get; set; }
    public bool GalgamePageNewLayout_ShowRating { get; set; }
    public bool GalgamePageNewLayout_ShowTags { get; set; }
    public bool GalgamePageNewLayout_ShowCharacters { get; set; }

    public ManageGalgamePageLayoutDialog()
    {
        InitializeComponent();
        RequestedTheme = App.MainWindow?.Content is Microsoft.UI.Xaml.FrameworkElement element ? element.RequestedTheme : RequestedTheme;
        XamlRoot = App.MainWindow!.Content.XamlRoot;
        Title = "ManageGalgamePageLayoutDialog_Title".GetLocalized();
        PrimaryButtonText = "Yes".GetLocalized();
        SecondaryButtonText = "Cancel".GetLocalized();

        // 明确设置宽度属性以覆盖默认样式限制
        MinWidth = 600;
        Width = 600;

        LoadSettings();
        PrimaryButtonClick += ManageGalgamePageLayoutDialog_PrimaryButtonClick;

        // 监听主标题和副标题的选择变化，确保它们不同
        PrimaryTitleComboBox.SelectionChanged += TitleComboBox_SelectionChanged;
        SecondaryTitleComboBox.SelectionChanged += TitleComboBox_SelectionChanged;
    }

    private void TitleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 检查两个ComboBox的SelectedItem是否为null，如果有一个为null则直接返回
        if (PrimaryTitleComboBox.SelectedItem == null || SecondaryTitleComboBox.SelectedItem == null)
        {
            return;
        }

        // 获取当前选择的DisplayName枚举值
        DisplayName primaryType = (DisplayName)PrimaryTitleComboBox.SelectedItem;
        DisplayName secondaryType = (DisplayName)SecondaryTitleComboBox.SelectedItem;

        // 只有当两个下拉框都选择了相同值（且不是"无"）时才需要处理冲突
        if (primaryType == secondaryType && secondaryType != DisplayName.None)
        {
            // 如果是副标题被改变，则将副标题设为不同于主标题的值
            if (ReferenceEquals(sender, SecondaryTitleComboBox))
            {
                // 找到一个不同的值
                DisplayName newValue = PrimaryTitleTypes.FirstOrDefault(t => t != primaryType && t != DisplayName.None);
                if (newValue != DisplayName.None)
                {
                    SecondaryTitleComboBox.SelectedItem = newValue;
                }
                else
                {
                    SecondaryTitleComboBox.SelectedItem = DisplayName.None;
                }
            }
            // 如果是主标题被改变，则将副标题设为不同的值
            else if (ReferenceEquals(sender, PrimaryTitleComboBox))
            {
                // 找到一个不同的值
                DisplayName newValue = SecondaryTitleTypes.FirstOrDefault(t => t != primaryType && t != DisplayName.None);
                if (newValue != DisplayName.None)
                {
                    SecondaryTitleComboBox.SelectedItem = newValue;
                }
                else
                {
                    SecondaryTitleComboBox.SelectedItem = DisplayName.None;
                }
            }
        }
    }

    private async void LoadSettings()
    {
        // 直接读取DisplayName枚举值
        GalgamePagePrimaryTitleType = await _localSettingsService.ReadSettingAsync<DisplayName>(KeyValues.GalgamePagePrimaryTitleType);
        GalgamePageSecondaryTitleType = await _localSettingsService.ReadSettingAsync<DisplayName>(KeyValues.GalgamePageSecondaryTitleType);

        GalgamePageNewLayout_ShowPainter = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout_ShowPainter);
        GalgamePageNewLayout_ShowSeiyu = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout_ShowSeiyu);
        GalgamePageNewLayout_ShowWriter = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout_ShowWriter);
        GalgamePageNewLayout_ShowMusician = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout_ShowMusician);
        GalgamePageNewLayout_ShowBackground = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout_ShowHeaderImage);
        GalgamePageNewLayout_ShowCover = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout_CoverImage);
        GalgamePageNewLayout_ShowCoverWhenNoBackground = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout_ShowCoverWhenNoBackground);
        GalgamePageNewLayout_ShowExpectedPlayTime = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout_ShowExpectedPlayTime);
        GalgamePageNewLayout_ShowRating = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout_ShowRating);
        GalgamePageNewLayout_ShowTags = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout_ShowTags);
        GalgamePageNewLayout_ShowCharacters = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.GalgamePageNewLayout_ShowCharacters);
    }

    private async void ManageGalgamePageLayoutDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();

        // 直接保存DisplayName枚举值
        await _localSettingsService.SaveSettingAsync(KeyValues.GalgamePagePrimaryTitleType, GalgamePagePrimaryTitleType);
        await _localSettingsService.SaveSettingAsync(KeyValues.GalgamePageSecondaryTitleType, GalgamePageSecondaryTitleType);
        await _localSettingsService.SaveSettingAsync(KeyValues.GalgamePageNewLayout_ShowPainter, GalgamePageNewLayout_ShowPainter);
        await _localSettingsService.SaveSettingAsync(KeyValues.GalgamePageNewLayout_ShowSeiyu, GalgamePageNewLayout_ShowSeiyu);
        await _localSettingsService.SaveSettingAsync(KeyValues.GalgamePageNewLayout_ShowWriter, GalgamePageNewLayout_ShowWriter);
        await _localSettingsService.SaveSettingAsync(KeyValues.GalgamePageNewLayout_ShowMusician, GalgamePageNewLayout_ShowMusician);
        await _localSettingsService.SaveSettingAsync(KeyValues.GalgamePageNewLayout_ShowHeaderImage, GalgamePageNewLayout_ShowBackground);
        await _localSettingsService.SaveSettingAsync(KeyValues.GalgamePageNewLayout_CoverImage, GalgamePageNewLayout_ShowCover);
        await _localSettingsService.SaveSettingAsync(KeyValues.GalgamePageNewLayout_ShowCoverWhenNoBackground, GalgamePageNewLayout_ShowCoverWhenNoBackground);
        await _localSettingsService.SaveSettingAsync(KeyValues.GalgamePageNewLayout_ShowExpectedPlayTime, GalgamePageNewLayout_ShowExpectedPlayTime);
        await _localSettingsService.SaveSettingAsync(KeyValues.GalgamePageNewLayout_ShowRating, GalgamePageNewLayout_ShowRating);
        await _localSettingsService.SaveSettingAsync(KeyValues.GalgamePageNewLayout_ShowTags, GalgamePageNewLayout_ShowTags);
        await _localSettingsService.SaveSettingAsync(KeyValues.GalgamePageNewLayout_ShowCharacters, GalgamePageNewLayout_ShowCharacters);

        deferral.Complete();
    }
}

