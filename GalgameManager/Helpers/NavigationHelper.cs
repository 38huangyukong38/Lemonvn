using System.Diagnostics;
using GalgameManager.Contracts.Services;
using GalgameManager.Models;
using GalgameManager.Models.Filters;
using GalgameManager.ViewModels;
using GalgameManager.WinApp.Base.Contracts.NavigationApi.NavigateParameters;
using GalgameManager.WinApp.Base.Models.Filters;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SourceFilter = GalgameManager.Models.Filters.SourceFilter;

namespace GalgameManager.Helpers;

// Helper class to set the navigation target for a NavigationViewItem.
//
// Usage in XAML:
// <NavigationViewItem x:Uid="Shell_Main" Icon="Document" helpers:NavigationHelper.NavigateTo="AppName.ViewModels.MainViewModel" />
//
// Usage in code:
// NavigationHelper.SetNavigateTo(navigationViewItem, typeof(MainViewModel).FullName);
public class NavigationHelper
{
    public static string GetNavigateTo(NavigationViewItem item) => (string)item.GetValue(NavigateToProperty);

    public static void SetNavigateTo(NavigationViewItem item, string value) => item.SetValue(NavigateToProperty, value);

    public static readonly DependencyProperty NavigateToProperty =
        DependencyProperty.RegisterAttached("NavigateTo", typeof(string), typeof(NavigationHelper), new PropertyMetadata(null));

    /// <summary>
    /// 导航到主页（游戏列表页）
    /// </summary>
    /// <param name="navigationService"></param>
    /// <param name="filterService">若需要添加filter，则填入filterService</param>
    /// <param name="filters">要添加的filter列表</param>
    public static void NavigateToHomePage(INavigationService navigationService, IFilterService? filterService = null,
        IEnumerable<FilterBase>? filters = null)
    {
        Debug.Assert(!(filterService is null ^ filters is null)); // 同时为null或同时不为null
        if (filterService is not null && filters is not null)
        {
            filterService.ClearFilters();
            foreach (FilterBase filter in filters)
            {
                filterService.AddFilter(filter);
                if (filter is CategoryFilter c)
                    c.Category.LastClicked = DateTime.Now;
                if (filter is SourceFilter s)
                    s.Source.LastClicked = DateTime.Now;
                    
            }
        }
        navigationService.NavigateTo(typeof(HomeViewModel).FullName!);
    }

    /// <summary>
    /// 导航到游戏详情页
    /// </summary>
    /// <param name="navigationService"></param>
    /// <param name="parameter"></param>
    public static void NavigateToGalgamePage(INavigationService navigationService, GalgamePageParameter parameter)
    {
        navigationService.SetListDataItemForNextConnectedAnimation(parameter.Galgame);
        navigationService.NavigateTo(typeof(GalgameViewModel).FullName!, parameter);
    }
    
    /// <summary>
    /// 导航到游戏设置页
    /// </summary>
    /// <param name="navigationService"></param>
    /// <param name="target"></param>
    public static void NavigateToGalgameSettingPage(INavigationService navigationService, Galgame target)
    {
        navigationService.NavigateTo(typeof(GalgameSettingViewModel).FullName!, target);
    }

    public static void NavigateToStaffPage(INavigationService navigationService,
        StaffPageNavParameter parameter)
    {
        navigationService.NavigateTo(typeof(StaffViewModel).FullName!, parameter);
    }
}
