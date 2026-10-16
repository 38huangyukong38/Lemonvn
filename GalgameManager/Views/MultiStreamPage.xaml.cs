using GalgameManager.Models;
using GalgameManager.Models.Sources;
using GalgameManager.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views;

public sealed partial class MultiStreamPage : Page
{
    public MultiStreamViewModel ViewModel
    {
        get;
    }

    public MultiStreamPage()
    {
        ViewModel = App.GetService<MultiStreamViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
    }

    // 使用Bind ElementName的方式无法绑定到viewmodel的command，临时workaround
    private void ClickGame(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement container && container.DataContext != null)
            ViewModel.ClickGameCommand.Execute(container.DataContext as Galgame);
    }

    private void ClickCategory(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement container && container.DataContext != null)
            ViewModel.ClickCategoryCommand.Execute(container.DataContext as Category);
    }

    private void ClickSource(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement container && container.DataContext != null)
            ViewModel.ClickSourceCommand.Execute(container.DataContext as GalgameSourceBase);
    }
}
