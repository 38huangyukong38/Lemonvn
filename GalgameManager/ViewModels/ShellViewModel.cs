using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace GalgameManager.ViewModels;

public partial class ShellViewModel : ObservableObject
{
    [ObservableProperty] private bool _isBackEnabled;
    [ObservableProperty] private object? _selected;
    [ObservableProperty] private bool _isDeveloperMode;
    private int _unreadInfos;
    private readonly IBgTaskService _bgTaskService;
    private string? _forcedTitle;
    [ObservableProperty] private bool _updateBadgeVisibility;
    [ObservableProperty] private string? _title;
    [ObservableProperty] private bool _infoPageBadgeVisibility;
    [ObservableProperty] private int _infoPageBadgeCount;

    public string? SelectedPageTitle
    {
        get
        {
            if (_forcedTitle != null) return _forcedTitle;
            if (Selected is NavigationViewItem navigationViewItem)
            {
                // 导航项的 Content 是一个 Grid
                if (navigationViewItem.Content is Grid grid)
                {
                    // 找到 Grid 中的 TextBlock
                    foreach (var child in grid.Children)
                    {
                        if (child is TextBlock textBlock)
                        {
                            return textBlock.Text;
                        }
                    }
                }
                // 对于隐藏的导航项，可能没有使用 Grid 结构
                else if (navigationViewItem.Content is string text)
                {
                    return text;
                }
            }
            return null;
        }
    }


    public INavigationService NavigationService
    {
        get;
    }

    public INavigationViewService NavigationViewService
    {
        get;
    }

    public ShellViewModel(INavigationService navigationService, INavigationViewService navigationViewService,
        IUpdateService updateService, IBgTaskService bgTaskService, ILocalSettingsService localSettingsService,
        IInfoService infoService)
    {
        NavigationService = navigationService;
        NavigationViewService = navigationViewService;
        _bgTaskService = bgTaskService;

        _isDeveloperMode = localSettingsService.ReadSettingAsync<bool>(KeyValues.DevelopmentMode).Result;

        NavigationService.Navigated += OnNavigated;
        updateService.SettingBadgeEvent += result => UpdateBadgeVisibility = result;
        _bgTaskService.BgTaskAdded += _ => UpdateInfoPageBadge();
        _bgTaskService.BgTaskRemoved += _ => UpdateInfoPageBadge();
        infoService.Infos.CollectionChanged += (_, _) =>
        {
            if(_isDeveloperMode == false) return;
            _unreadInfos = infoService.Infos.Count(info =>
                info.Severity is InfoBarSeverity.Warning or InfoBarSeverity.Error && info.Read == false);
            UpdateInfoPageBadge();
        };
        localSettingsService.OnSettingChanged += HandleLocalSettingChanged;
        infoService.OnInfo += (severity, title, msg, displayTime) =>
            _ = DisplayInfoMsgAsync(severity, title, msg, displayTime);
        infoService.OnEvent += (severity, title, msg, callbackAction, callbackButtonText) =>
            _ = DisplayEventMsgAsync(severity, title, msg, callbackAction: callbackAction, callbackButtonText: callbackButtonText);

        UpdateInfoPageBadge();
    }

    private void OnNavigated(object sender, NavigationEventArgs e)
    {
        IsBackEnabled = NavigationService.CanGoBack;
        _forcedTitle = NavigationService.Title;
        var selectedItem = NavigationViewService.GetSelectedItem(e.SourcePageType);
        if (selectedItem != null) Selected = selectedItem;
        OnPropertyChanged(nameof(SelectedPageTitle));

        IsInfoBarOpen = false;
    }

    private void HandleLocalSettingChanged(string key, object? value)
    {
        if (key == KeyValues.DevelopmentMode)
            IsDeveloperMode = value as bool? ?? false;
    }

    private void UpdateInfoPageBadge()
    {
        InfoPageBadgeCount = _bgTaskService.GetBgTasks().Count() + _unreadInfos;
        InfoPageBadgeVisibility = InfoPageBadgeCount > 0;
    }

    #region INFO_BAR_CTRL

    private int _infoBarIndex;
    [ObservableProperty] private bool _isInfoBarOpen;
    [ObservableProperty] private string? _infoBarTitle;
    [ObservableProperty] private string? _infoBarMessage;
    [ObservableProperty] private InfoBarSeverity _infoBarSeverity = InfoBarSeverity.Informational;
    public ObservableCollection<ShellEventViewModel> ShellEvents { get; } = new();

    private async Task DisplayInfoMsgAsync(InfoBarSeverity infoBarSeverity, string? title, string? msg,
        int delayMs = 3000)
    {
        if (title is null && msg is null)
        {
            IsInfoBarOpen = false;
            return;
        }

        var currentIndex = ++_infoBarIndex;
        InfoBarSeverity = infoBarSeverity;
        InfoBarTitle = title;
        InfoBarMessage = msg;
        IsInfoBarOpen = true;
        await Task.Delay(delayMs);
        if (currentIndex == _infoBarIndex)
            IsInfoBarOpen = false;
    }

    private async Task DisplayEventMsgAsync(InfoBarSeverity infoBarSeverity, string? title, string? msg,
        int delayMs = 3000, Action? callbackAction = null, string? callbackButtonText = null)
    {
        if (msg?.Length >= 200)
        {
            msg = msg[..200];
            if (msg.Count(c => c == '\n') > 1)
            {
                msg = string.Join('\n', msg.Split('\n').Take(2));
                msg = msg[..^4];
            }
            msg += "...";
        }

        if(ShellEvents.Count > 3) ShellEvents.RemoveAt(0); // 最多同时显示3个事件
        ShellEventViewModel shellEvent = new()
        {
            Title = title,
            Message = msg,
            Severity = infoBarSeverity,
            CallbackAction = callbackAction ?? NavigateToInfoPage,
            CallbackButtonText = callbackButtonText ?? "ShellPage_Info_Event".GetLocalized(),
        };
        ShellEvents.Add(shellEvent);
        await Task.Delay(delayMs);
        ShellEvents.Remove(shellEvent);
    }

    [RelayCommand]
    private void NavigateToInfoPage()
    {
        NavigationService.NavigateTo(typeof(InfoViewModel).FullName!);
    }

    #endregion
}

public partial class ShellEventViewModel
{
    public string? Title;
    public string? Message;
    public InfoBarSeverity Severity;
    public Action? CallbackAction { get; init; }
    public string? CallbackButtonText { get; init; }

    [RelayCommand]
    private void ExecuteCallback() => CallbackAction?.Invoke();
}
