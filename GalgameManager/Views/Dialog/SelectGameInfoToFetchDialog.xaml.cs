using GalgameManager.Enums;
using GalgameManager.Helpers;
using Microsoft.UI.Xaml;

namespace GalgameManager.Views.Dialog;

public sealed partial class SelectGameInfoToFetchDialog
{
    /// <summary>
    /// 是否被取消了
    /// </summary>
    public bool Canceled = true;
    
    /// <summary>
    /// 选择的搜刮信息类型
    /// </summary>
    public GameParseType SelectedParseTypes { get; private set; } = GameParseType.All;

    public bool IncludingSubSources { get; private set; }

    /// <summary>
    /// 选择搜刮信息类型对话框
    /// </summary>
    public SelectGameInfoToFetchDialog(bool showIncludingSubSourcesCheckBox = true)
    {
        InitializeComponent();
        RequestedTheme = App.MainWindow?.Content is FrameworkElement element ? element.RequestedTheme : RequestedTheme;
        XamlRoot = App.MainWindow!.Content!.XamlRoot;
        PrimaryButtonText = "SelectGameInfoToFetchDialog_Fetch".GetLocalized();
        SecondaryButtonText = "Cancel".GetLocalized();
        Title = "SelectGameInfoToFetchDialog_Title".GetLocalized();

        if (!showIncludingSubSourcesCheckBox)
        {
            Line.Visibility = Visibility.Collapsed;
            IncludingSubSourcesCheckBox.Visibility = Visibility.Collapsed;
        }
        
        PrimaryButtonClick += (_, _) =>
        {
            SelectedParseTypes = 0;
            Canceled = false;
            
            if (GameInfoCheckBox.IsChecked == true)
                SelectedParseTypes |= GameParseType.GameInfo;
            if (HeaderImageCheckBox.IsChecked == true)
                SelectedParseTypes |= GameParseType.HeaderImage;
            if (ImageCheckBox.IsChecked == true)
                SelectedParseTypes |= GameParseType.Image;
            if (CharacterCheckBox.IsChecked == true)
                SelectedParseTypes |= GameParseType.Character;
            if (PlayStatusCheckBox.IsChecked == true)
                SelectedParseTypes |= GameParseType.PlayStatus;
            
            IncludingSubSources = IncludingSubSourcesCheckBox.IsChecked == true;
        };
    }
}
