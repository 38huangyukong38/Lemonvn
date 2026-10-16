using Windows.System;
using Microsoft.UI.Xaml.Controls;
using GalgameManager.Models;
using CommunityToolkit.WinUI;

namespace GalgameManager.Views.Dialog;

public enum SavePositionDialogResult
{
    OpenExplorer,
    UseStandardPicker,
    Cancel
}

public sealed partial class SavePositionDialog : ContentDialog
{
    public SavePositionDialogResult Result { get; private set; }
    public string SelectedPath { get; private set; } = string.Empty;

    public SavePositionDialog(Galgame galgame, string initialPath, bool hasDetectedSavePosition)
    {
        InitializeComponent();

        Title = "SavePositionDialog_Title".GetLocalized();
        XamlRoot = App.MainWindow!.Content.XamlRoot;
        DefaultButton = ContentDialogButton.Primary;

        // 根据是否有检测到的存档位置来设置提示文本
        if (hasDetectedSavePosition)
        {
            InstructionTextBlock.Text = "SavePositionDialog_BrowseExistingPath_Title".GetLocalized();
            HelperTextBlock.Text = "SavePositionDialog_BrowseExistingPath_Helper".GetLocalized();
            PathTextBlock.Text = initialPath;
        }
        else
        {
            InstructionTextBlock.Text = "SavePositionDialog_FindNewPath_Title".GetLocalized();
            HelperTextBlock.Text = "SavePositionDialog_FindNewPath_Helper".GetLocalized();
            PathTextBlock.Text = "GalgameSettingPage_DetectedSavePosition".GetLocalized();
        }

        // 设置按钮文本
        PrimaryButtonText = "SavePositionDialog_OpenExplorer".GetLocalized();
        SecondaryButtonText = "SavePositionDialog_UseStandardPicker".GetLocalized();
        CloseButtonText = "SavePositionDialog_Cancel".GetLocalized();

        // 保存初始路径
        SelectedPath = initialPath;

        // 注册事件处理程序
        PrimaryButtonClick += OnPrimaryButtonClicked;
        SecondaryButtonClick += OnSecondaryButtonClicked;
        CloseButtonClick += OnCloseButtonClicked;
    }

    private void OnPrimaryButtonClicked(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        Result = SavePositionDialogResult.OpenExplorer;

        // 打开资源管理器到指定路径
        try
        {
            Launcher.LaunchUriAsync(new Uri(SelectedPath)).AsTask().Wait();
        }
        catch
        {
            // 忽略打开资源管理器失败的情况
        }
    }

    private void OnSecondaryButtonClicked(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        Result = SavePositionDialogResult.UseStandardPicker;
    }

    private void OnCloseButtonClicked(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        Result = SavePositionDialogResult.Cancel;
    }
}