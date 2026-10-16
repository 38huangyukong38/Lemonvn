using System.Collections.ObjectModel;
using GalgameManager.Helpers;
using GalgameManager.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views.Dialog;

public sealed partial class EditPlayTimeDialog : ContentDialog
{
    private readonly ObservableCollection<DisplayPlayTime> _playTimes = new();
    private readonly Galgame _galgame;

    public EditPlayTimeDialog(Galgame galgame)
    {
        _galgame = galgame; // Store the galgame object
        InitializeComponent();
        RequestedTheme = App.MainWindow?.Content is Microsoft.UI.Xaml.FrameworkElement element ? element.RequestedTheme : RequestedTheme;

        XamlRoot = App.MainWindow!.Content.XamlRoot;
        Title = "EditPlayTimeDialog_Title".GetLocalized();
        PrimaryButtonText = "Yes".GetLocalized();
        SecondaryButtonText = "Cancel".GetLocalized();

        PlayCountNumberBox.Value = _galgame.PlayCount; // Initialize NumberBox

        foreach (var (date, playedTime) in _galgame.PlayedTime)
        {
            DisplayPlayTime displayPlayTime = new()
            {
                Date = date,
                PlayedTime = playedTime
            };
            _playTimes.Add(displayPlayTime);
        }
        ListView.ItemsSource = _playTimes;

        PrimaryButtonClick += (_, _) =>
        {
            // PlayCount is already updated by PlayCountNumberBox_OnValueChanged
            _galgame.PlayedTime.Clear();
            var totalTime = 0;
            
            foreach (DisplayPlayTime time in _playTimes)
            {
                if (time.PlayedTime > 0)
                {
                    _galgame.PlayedTime.Add(time.Date, time.PlayedTime);
                    totalTime += time.PlayedTime;
                }
            }
            
            _galgame.TotalPlayTime = totalTime;
            
            // LastPlayTime 现在是自动计算的，不需要手动设置
        };
    }

    private void PlayCountNumberBox_OnValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_galgame != null)
        {
            // Ensure the value is not NaN, which can happen if the box is cleared. Default to 0.
            _galgame.PlayCount = (int)(double.IsNaN(args.NewValue) ? 0 : args.NewValue);
        }
    }

    private void DatePickerFlyout_OnDatePicked(DatePickerFlyout sender, DatePickedEventArgs args)
    {
        if (_playTimes.Any(time => time.Date == sender.Date.ToString("yyyy/M/d"))) return;
        DisplayPlayTime newTime = new()
        {
            Date = sender.Date.ToString("yyyy/M/d"),
            PlayedTime = 0
        };
        foreach (DisplayPlayTime time in _playTimes)
            if (newTime < time)
            {
                _playTimes.Insert(_playTimes.IndexOf(time), newTime);
                return;
            }
        _playTimes.Add(newTime);
    }

    private void ButtonDelete_OnClick(object sender, RoutedEventArgs e)
    {
        if (ListView.SelectedItem is not DisplayPlayTime time) return;
        _playTimes.Remove(time);
    }
}

public class DisplayPlayTime
{
    public string Date = string.Empty;
    public int PlayedTime;

    public static bool operator < (DisplayPlayTime x, DisplayPlayTime y)
    {
        try
        {
            var arrX = x.Date.Split('/');
            var arrY = y.Date.Split('/');
            
            // 确保日期数组有足够的元素
            if (arrX.Length < 3 || arrY.Length < 3)
                return string.Compare(x.Date, y.Date, StringComparison.Ordinal) < 0;
                
            if (int.Parse(arrX[0]) != int.Parse(arrY[0])) return int.Parse(arrX[0]) < int.Parse(arrY[0]);
            if (int.Parse(arrX[1]) != int.Parse(arrY[1])) return int.Parse(arrX[1]) < int.Parse(arrY[1]);
            return int.Parse(arrX[2]) < int.Parse(arrY[2]);
        }
        catch (Exception)
        {
            return string.Compare(x.Date, y.Date, StringComparison.Ordinal) < 0; // 使用字符串比较作为备选方案
        }
    }

    public static bool operator > (DisplayPlayTime x, DisplayPlayTime y)
    {
        return !(x < y);
    }
}
