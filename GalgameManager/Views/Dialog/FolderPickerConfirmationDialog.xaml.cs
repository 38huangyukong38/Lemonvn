using Microsoft.UI.Xaml.Controls;
using GalgameManager.Helpers;

namespace GalgameManager.Views.Dialog;

public enum FolderPickerConfirmationResult
{
    ShowPicker,
    Skip,
    Cancel
}

public sealed partial class FolderPickerConfirmationDialog : ContentDialog
{
    public FolderPickerConfirmationResult Result { get; private set; }

    public FolderPickerConfirmationDialog()
    {
        InitializeComponent();

        Title = "FolderPickerConfirmationDialog_Title".GetLocalized();
        XamlRoot = App.MainWindow!.Content.XamlRoot;
        DefaultButton = ContentDialogButton.Secondary;

        // 设置按钮文本
        PrimaryButtonText = "FolderPickerConfirmationDialog_ShowPicker".GetLocalized();
        SecondaryButtonText = "FolderPickerConfirmationDialog_Skip".GetLocalized();
        CloseButtonText = "FolderPickerConfirmationDialog_Cancel".GetLocalized();

        // 注册事件处理程序
        PrimaryButtonClick += OnPrimaryButtonClicked;
        SecondaryButtonClick += OnSecondaryButtonClicked;
        CloseButtonClick += OnCloseButtonClicked;
    }

    private void OnPrimaryButtonClicked(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        Result = FolderPickerConfirmationResult.ShowPicker;
    }

    private void OnSecondaryButtonClicked(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        Result = FolderPickerConfirmationResult.Skip;
    }

    private void OnCloseButtonClicked(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        Result = FolderPickerConfirmationResult.Cancel;
    }
}