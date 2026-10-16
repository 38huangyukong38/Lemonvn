using GalgameManager.Contracts.Services;
using GalgameManager.Models;
using GalgameManager.Services;
using GalgameManager.ViewModels;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace GalgameManager.Views;

public sealed partial class PluginHostPage : Page
{
    public PluginHostPageViewModel ViewModel { get; }

    public PluginHostPage()
    {
        ViewModel = App.GetService<PluginHostPageViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is not PluginPageNavigationParameter parameter)
        {
            PluginContent.Content = null;
            App.GetService<IInfoService>().DeveloperEvent(msg: "PluginHostPage received an invalid navigation parameter.");
            return;
        }

        ShowPluginPage(parameter);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        PluginContent.Content = null;
        base.OnNavigatedFrom(e);
    }

    private void ShowPluginPage(PluginPageNavigationParameter navigationParameter)
    {
        IInfoService infoService = App.GetService<IInfoService>();

        try
        {
            PluginX plugin = App.GetService<IPluginService>().GetAllPluginsAsync().GetAwaiter().GetResult()
                .FirstOrDefault(p => p.Id == navigationParameter.PluginInfo.Id && p.Plugin is not null)
                ?? throw new InvalidOperationException($"Plugin {navigationParameter.PluginInfo.Name} is not loaded.");

            Type pageType = plugin.Plugin!.GetType().Assembly.GetType(navigationParameter.PageTypeFullName)
                ?? throw new InvalidOperationException($"Page type {navigationParameter.PageTypeFullName} was not found.");

            if (typeof(Page).IsAssignableFrom(pageType) == false)
                throw new InvalidOperationException($"Page type {navigationParameter.PageTypeFullName} is not a Page.");

            using IDisposable scope = PluginXamlHost.EnterScope(plugin.Plugin.GetType().Assembly);
            if (Activator.CreateInstance(pageType) is not Page page)
                throw new InvalidOperationException($"Failed to create page {navigationParameter.PageTypeFullName}.");

            page.HorizontalAlignment = HorizontalAlignment.Stretch;
            page.VerticalAlignment = VerticalAlignment.Stretch;
            PluginContent.Content = page;
        }
        catch (Exception ex)
        {
            PluginContent.Content = null;
            infoService.PluginEvent(navigationParameter.PluginInfo, ex);
        }
    }
}
