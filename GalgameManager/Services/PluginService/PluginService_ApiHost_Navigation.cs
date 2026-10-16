using GalgameManager.Contracts.Services;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.ViewModels;
using GalgameManager.Views;
using GalgameManager.WinApp.Base.Contracts;
using GalgameManager.WinApp.Base.Contracts.NavigationApi;
using GalgameManager.WinApp.Base.Contracts.NavigationApi.NavigateParameters;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Services;

public partial class PluginService
{
    public partial class PotatoVnApiHost : IPotatoVnApi
    {
        private readonly INavigationService _navigationService = App.GetService<INavigationService>();

        public Window? GetMainWindow() => App.MainWindow;

        public void NavigateTo(PageEnum page, object? parameter = null)
            => InvokeOnUiThread(() => NavigateToBuiltInPage(page, parameter));

        public void NavigateTo(Type pageType, string? title = null, object? parameter = null,
            bool clearNavigation = false)
        {
            ArgumentNullException.ThrowIfNull(pageType);

            if (typeof(Page).IsAssignableFrom(pageType) == false)
                throw new ArgumentException("PluginService_ApiHost_PageTypeFault".GetLocalized(), nameof(pageType));
            if (plugin.Plugin is null) //不应该发生
                throw new InvalidOperationException("Plugin is not loaded.");

            Type pluginType = plugin.Plugin.GetType();
            if (pageType.Assembly != pluginType.Assembly)
                throw new InvalidOperationException("PageType must belong to the current plugin assembly.");

            var pageTypeFullName = pageType.FullName ?? throw new ArgumentException("pageType must have a full name.");
            var resolvedTitle = string.IsNullOrWhiteSpace(title) ? plugin.Info.Name : title;
            PluginPageNavigationParameter navParameter = new()
            {
                PluginInfo = plugin.Info.ShallowClone(),
                PageTypeFullName = pageTypeFullName,
                Parameter = parameter,
            };
            InvokeOnUiThread(() =>
            {
                _navigationService.NavigateTo(typeof(PluginHostPageViewModel).FullName!, navParameter, clearNavigation, resolvedTitle);
            });
        }

        private void NavigateToBuiltInPage(PageEnum page, object? parameter = null)
        {
            switch (page)
            {
                case PageEnum.CategoryPage:
                    _navigationService.NavigateTo(typeof(CategoryViewModel).FullName!);
                    break;
                case PageEnum.GalgamePage:
                    if (parameter is not GalgamePageNavParameter navParameter)
                        throw new ArgumentException(null, nameof(parameter));
                    _navigationService.NavigateTo(typeof(GalgameViewModel).FullName!, new GalgamePageParameter
                    {
                        Galgame =  navParameter.Galgame,
                        ForceStartGame = navParameter.StartGame,
                    });
                    break;
                case PageEnum.GameListPage:
                    _navigationService.NavigateTo(typeof(HomeViewModel).FullName!);
                    break;
                case PageEnum.HomePage:
                    _navigationService.NavigateTo(typeof(MultiStreamViewModel).FullName!);
                    break;
                case PageEnum.LibraryPage:
                    LibraryPageNavParameter? param = parameter as LibraryPageNavParameter;
                    _navigationService.NavigateTo(typeof(LibraryViewModel).FullName!, param?.TargetSource);
                    break;
                case PageEnum.SettingsPage:
                    _navigationService.NavigateTo(typeof(SettingsViewModel).FullName!);
                    break;
                case PageEnum.PluginPage:
                    _navigationService.NavigateTo(typeof(PluginViewModel).FullName!);
                    break;
                case PageEnum.PluginStorePage:
                    _navigationService.NavigateTo(typeof(PluginStoreViewModel).FullName!);
                    break;
                case PageEnum.AccountPage:
                    _navigationService.NavigateTo(typeof(AccountViewModel).FullName!);
                    break;
                case PageEnum.InfoPage:
                    _navigationService.NavigateTo(typeof(InfoViewModel).FullName!);
                    break;
                case PageEnum.StaffPage:
                    if (parameter is not StaffPageNavParameter staffParam)
                        throw new ArgumentException(null, nameof(parameter));
                    _navigationService.NavigateTo(typeof(StaffViewModel).FullName!, staffParam);
                    break;
                case PageEnum.PlayedTimePage:
                    if (parameter is not Galgame playedTimeTarget)
                        throw new ArgumentException(null, nameof(parameter));
                    _navigationService.NavigateTo(typeof(PlayedTimeViewModel).FullName!, playedTimeTarget);
                    break;
                case PageEnum.ScanResultPage:
                    if (parameter is not Guid scanResultId)
                        throw new ArgumentException(null, nameof(parameter));
                    _navigationService.NavigateTo(typeof(ScanResultViewModel).FullName!, scanResultId);
                    break;
                case PageEnum.HelpPage:
                    _navigationService.NavigateTo(typeof(HelpViewModel).FullName!);
                    break;
                case PageEnum.UpdateContentPage:
                    if (parameter is bool displayTitle)
                        _navigationService.NavigateTo(typeof(UpdateContentViewModel).FullName!, displayTitle);
                    else
                        _navigationService.NavigateTo(typeof(UpdateContentViewModel).FullName!);
                    break;
                case PageEnum.AnnualReportPage:
                    _navigationService.NavigateTo(typeof(AnnualReportViewModel).FullName!);
                    break;
                case PageEnum.CategorySettingPage:
                    if (parameter is not Category category)
                        throw new ArgumentException(null, nameof(parameter));
                    _navigationService.NavigateTo(typeof(CategorySettingViewModel).FullName!, category);
                    break;
                case PageEnum.GalgameSettingPage:
                    if (parameter is not Galgame galgame)
                        throw new ArgumentException(null, nameof(parameter));
                    _navigationService.NavigateTo(typeof(GalgameSettingViewModel).FullName!, galgame);
                    break;
                case PageEnum.GalgameSourcePage:
                    _navigationService.NavigateTo(typeof(GalgameSourceViewModel).FullName!, parameter);
                    break;
                case PageEnum.GalgameCharacterPage:
                    if (parameter is not GalgameCharacterPageNavParameter characterParam)
                        throw new ArgumentException(null, nameof(parameter));
                    _navigationService.NavigateTo(typeof(GalgameCharacterViewModel).FullName!, characterParam);
                    break;
                case PageEnum.MultiStreamPage:
                    if (parameter is bool retry)
                        _navigationService.NavigateTo(typeof(MultiStreamViewModel).FullName!, retry);
                    else
                        _navigationService.NavigateTo(typeof(MultiStreamViewModel).FullName!);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(page), page, null);
            }
        }

        private static void InvokeOnUiThread(Action action)
        {
            if (App.DispatcherQueue.HasThreadAccess)
            {
                action();
                return;
            }
            UiThreadInvokeHelper.InvokeAsync(action).GetAwaiter().GetResult();
        }
    }
}
