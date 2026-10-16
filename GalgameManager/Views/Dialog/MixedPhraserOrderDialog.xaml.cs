using System.Collections.ObjectModel;
using System.Globalization;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Helpers.Phrase;
using Microsoft.UI.Xaml;

namespace GalgameManager.Views.Dialog;

public sealed partial class MixedPhraserOrderDialog
{
    private readonly MixedPhraserOrder _order;
    public List<MixedPhraserOrderDialogItem> Items { get; set; } = new();

    public MixedPhraserOrderDialog(MixedPhraserOrder order)
    {
        InitializeComponent();
        RequestedTheme = App.MainWindow?.Content is FrameworkElement element ? element.RequestedTheme : RequestedTheme;

        XamlRoot = App.MainWindow!.Content.XamlRoot;
        Title = "MixedPhraserOrderDialog_Title".GetLocalized();
        PrimaryButtonText = "Yes".GetLocalized();
        SecondaryButtonText = "Cancel".GetLocalized();

        _order = order;
        InitializeItems();
    }
    
    private void InitializeItems()
    {
        Items = new()
        {
            new MixedPhraserOrderDialogItem("MixedPhraserOrderDialog_It_Name".GetLocalized(), _order.NameOrder),
            new MixedPhraserOrderDialogItem("MixedPhraserOrderDialog_It_Des".GetLocalized(), _order.DescriptionOrder),
            new MixedPhraserOrderDialogItem("MixedPhraserOrderDialog_It_Exp".GetLocalized(), _order.ExpectedPlayTimeOrder),
            new MixedPhraserOrderDialogItem("MixedPhraserOrderDialog_It_Rating".GetLocalized(), _order.RatingOrder),
            new MixedPhraserOrderDialogItem("MixedPhraserOrderDialog_It_Image".GetLocalized(), _order.ImageUrlOrder),
            new MixedPhraserOrderDialogItem("MixedPhraserOrderDialog_It_ReleaseDate".GetLocalized(), _order.ReleaseDateOrder),
            new MixedPhraserOrderDialogItem("MixedPhraserOrderDialog_It_Character".GetLocalized(), _order.CharactersOrder),
            new MixedPhraserOrderDialogItem("MixedPhraserOrderDialog_It_CnName".GetLocalized(), _order.CnNameOrder),
            new MixedPhraserOrderDialogItem("MixedPhraserOrderDialog_It_Dev".GetLocalized(), _order.DeveloperOrder),
            new MixedPhraserOrderDialogItem("MixedPhraserOrderDialog_It_Engine".GetLocalized(), _order.EngineOrder),
            new MixedPhraserOrderDialogItem("MixedPhraserOrderDialog_It_Tag".GetLocalized(), _order.TagsOrder),
            new MixedPhraserOrderDialogItem("MixedPhraserOrderDialog_It_Staff".GetLocalized(), _order.StaffOrder),
        };
        
        // 强制绑定更新
        Bindings.Update();
    }
    
    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        bool isChineseCulture = CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
        _order.SetToDefault(isChineseCulture);
        
        // 重新初始化 Items
        InitializeItems();
    }
}

public class MixedPhraserOrderDialogItem
{
    public string Title { get; }
    public ObservableCollection<RssType> Order { get; }

    public MixedPhraserOrderDialogItem(string title, ObservableCollection<RssType> order)
    {
        Title = title;
        Order = order;
    }
}