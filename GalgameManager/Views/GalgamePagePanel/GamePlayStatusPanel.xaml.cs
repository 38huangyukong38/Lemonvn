using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Models;
using GalgameManager.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;

namespace GalgameManager.Views.GalgamePagePanel;

public partial class GamePlayStatusPanel
{
    private readonly IInfoService _infoService = App.GetService<IInfoService>();
    private readonly INavigationService _navigationService = App.GetService<INavigationService>();
    private readonly ILocalSettingsService _localSettingsService = App.GetService<ILocalSettingsService>();

    public GamePlayStatusPanel()
    {
        InitializeComponent();
    }

    protected override void Update()
    {

    }

    private async void ChangeTimeFormat(object sender, RightTappedRoutedEventArgs e)
    {
        try
        {
            var current = await _localSettingsService.ReadSettingAsync<bool>(KeyValues.TimeAsHour);
            await _localSettingsService.SaveSettingAsync(KeyValues.TimeAsHour, !current);
            Game!.RaisePropertyChanged(nameof(Galgame.TotalPlayTime));
        }
        catch (Exception ex)
        {
            _infoService.Event(EventType.PageError, InfoBarSeverity.Error, "Oops, something went wrong", ex);
        }
    }

    private void ClickTotalPlayTime(object sender, RoutedEventArgs e)
    {
        _navigationService.NavigateTo(typeof(PlayedTimeViewModel).FullName!, Game);
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // 最上侧的信息栏有两种不通模式，一种是窄模式，一种是宽模式
        var isWide = e.NewSize.Width > 266;
        InfoStackPanel.Orientation = isWide ? Orientation.Horizontal : Orientation.Vertical;
        InfoStackPanel.Spacing = isWide ? 15 : 8;
        foreach (StackPanel child in InfoStackPanel.Children.OfType<StackPanel>())
        {
            child.Orientation = isWide ? Orientation.Vertical : Orientation.Horizontal;
            child.Margin = isWide ? new Thickness(0, 12, 0, 0) : new Thickness(0);
            child.Spacing = isWide ? 5 : 20;
        }
    }
}

public partial class RattingConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not int ratting || ratting == 0) return Galgame.DefaultString;
        return ratting.ToString("0.0");
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => 0; //不需要
}

public partial class CommentVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not string comment || string.IsNullOrWhiteSpace(comment)) return Visibility.Collapsed;
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => string.Empty;
}