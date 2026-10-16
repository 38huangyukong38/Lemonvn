using GalgameManager.Enums;
using GalgameManager.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views.Dialog;

public sealed partial class PvnBatchUploadDialog
{
    /// <summary>
    /// 是否被取消了
    /// </summary>
    public bool Canceled;

    /// <summary>
    /// 选择的上传属性
    /// </summary>
    public PvnUploadProperties SelectedProperties { get; private set; } = PvnUploadProperties.None;

    /// <summary>
    /// 批量上传选择对话框
    /// </summary>
    public PvnBatchUploadDialog()
    {
        InitializeComponent();
        RequestedTheme = App.MainWindow?.Content is FrameworkElement element ? element.RequestedTheme : RequestedTheme;
        XamlRoot = App.MainWindow!.Content!.XamlRoot;
        PrimaryButtonText = "PvnBatchUploadDialog_Upload".GetLocalized();
        SecondaryButtonText = "Cancel".GetLocalized();
        Title = "PvnBatchUploadDialog_Title".GetLocalized();
        
        PrimaryButtonClick += (_, _) =>
        {
            SelectedProperties = PvnUploadProperties.None;
            
            if (InfosCheckBox.IsChecked == true)
                SelectedProperties |= PvnUploadProperties.Infos;
            if (ImageLocCheckBox.IsChecked == true)
                SelectedProperties |= PvnUploadProperties.ImageLoc;
            if (ReviewCheckBox.IsChecked == true)
                SelectedProperties |= PvnUploadProperties.Review;
            if (PlayTimeCheckBox.IsChecked == true)
                SelectedProperties |= PvnUploadProperties.PlayTime;
            if (CharacterCheckBox.IsChecked == true)
                SelectedProperties |= PvnUploadProperties.Character;
            if (HeaderImageLocCheckBox.IsChecked == true)
                SelectedProperties |= PvnUploadProperties.HeaderImageLoc;
        };
        
        SecondaryButtonClick += (_, _) => Canceled = true;
    }
}
