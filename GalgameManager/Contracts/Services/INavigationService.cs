using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace GalgameManager.Contracts.Services;

public interface INavigationService
{
    event NavigatedEventHandler Navigated;

    bool CanGoBack
    {
        get;
    }

    Frame? Frame
    {
        get; set;
    }

    bool NavigateTo(string pageKey, object? parameter = null, bool clearNavigation = false, string title = "");

    /// <summary>
    /// 跳转到某个界面，其中pageType应该为typeof(界面（继承page的类）) <br/>
    /// 这个函数是给插件用的
    /// </summary>
    bool NavigateTo(Type pageType, string title = "", object? parameter = null, bool clearNavigation = false);

    bool GoBack();

    /// <summary>
    /// 当前导航页要的title，如果为null则表示用侧边栏的标题
    /// </summary>
    string? Title { get; }

    void SetListDataItemForNextConnectedAnimation(object item);
}
