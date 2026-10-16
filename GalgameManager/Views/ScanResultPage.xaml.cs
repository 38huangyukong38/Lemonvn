using GalgameManager.Models;
using GalgameManager.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views;

public sealed partial class ScanResultPage : Page
{
    public ScanResultViewModel ViewModel
    {
        get;
    }

    public ScanResultPage()
    {
        ViewModel = App.GetService<ScanResultViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
    }

    // ItemsRepeater模板无法可靠绑定页面命令，因此在代码隐藏中转发点击事件。
    private void CheckGame_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: PathScanResultItem item })
            ViewModel.CheckGameCommand.Execute(item);
    }

    // ItemsRepeater模板无法可靠绑定页面命令，因此在代码隐藏中转发点击事件。
    private void ConfirmLink_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: PathScanResultItem item })
            ViewModel.ConfirmLinkCommand.Execute(item);
    }
}
