using GalgameManager.Models.Sources;
using GalgameManager.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views;

public sealed partial class GalgameSettingPage : Page
{
    public GalgameSettingViewModel ViewModel { get; }

    public GalgameSettingPage()
    {
        ViewModel = App.GetService<GalgameSettingViewModel>();
        InitializeComponent();
    }

    // DataTemplate拥有独立名称范围，因此在代码隐藏中把点击事件转发给页面ViewModel。
    private void EditInstallationPath_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: GalgameAndPath installation })
        {
            ViewModel.SelectedInstallation = installation;
            ViewModel.SetGalgamePathCommand.Execute(null);
        }
    }

    // DataTemplate拥有独立名称范围，因此在代码隐藏中把点击事件转发给页面ViewModel。
    private void SetPreferredInstallation_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: GalgameAndPath installation })
            ViewModel.SetPreferredInstallationCommand.Execute(installation);
    }

    // DataTemplate拥有独立名称范围，因此在代码隐藏中把点击事件转发给页面ViewModel。
    private void OpenInstallationFolder_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: GalgameAndPath installation })
            ViewModel.OpenInstallationFolderCommand.Execute(installation);
    }

    // DataTemplate拥有独立名称范围，因此在代码隐藏中把点击事件转发给页面ViewModel。
    private void RemoveInstallation_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: GalgameAndPath installation })
            ViewModel.RemoveInstallationCommand.Execute(installation);
    }
}
