using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Contracts.Services;

public interface INavigationViewService
{
    void Initialize(NavigationView navigationView);

    void UnregisterEvents();

    NavigationViewItem? GetSelectedItem(Type pageType);
}
