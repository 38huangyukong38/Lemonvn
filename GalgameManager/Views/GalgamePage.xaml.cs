using GalgameManager.Contracts.Services;
using GalgameManager.Models.Sources;
using GalgameManager.ViewModels;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using CommunityToolkit.WinUI.Animations;

namespace GalgameManager.Views;

public sealed partial class HomeDetailPage : Page
{
    public GalgameViewModel ViewModel
    {
        get;
    }

    public HomeDetailPage()
    {
        ViewModel = App.GetService<GalgameViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        base.OnNavigatingFrom(e);
        if (e.SourcePageType == typeof(HomePage))
        {
            var navigationService = App.GetService<INavigationService>();

            if (ViewModel.Item != null)
            {
                navigationService.SetListDataItemForNextConnectedAnimation(ViewModel.Item);
            }
        }
    }

    // Flyout中的ItemsRepeater不会自动设置DataContext，模板按钮需显式绑定DataContext="{x:Bind}"，
    // 这里再从DataContext转发点击事件到页面命令。
    // 在“启动游戏”下拉中点击某个实例时，将其设为首选实例；保持弹窗打开以展示选中状态。
    private void SelectInstallation_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: GalgameAndPath installation })
            ViewModel.SetPreferredInstallationCommand.Execute(installation);
    }

    // 在“打开”下拉中点击某个实例时，打开对应安装目录。
    private void OpenInstallation_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: GalgameAndPath installation })
        {
            OpenInstallationFlyoutButton.Flyout.Hide();
            ViewModel.OpenInstallationInExplorerCommand.Execute(installation);
        }
    }
}
