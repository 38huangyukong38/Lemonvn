using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.ViewModels;
using System.Collections.ObjectModel;
using System.Diagnostics;
using GalgameManager.Enums;
using SkiaSharp;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.Kernel.Sketches;

namespace GalgameManager.ViewModels;
public partial class ReportSubPage1ViewModel : ObservableObject, INavigationAware
{
    [ObservableProperty] private AnnualReportData _annualReportData = new();
    
    [ObservableProperty] private int _mostActiveMonth = 0;
    [ObservableProperty] private double _averagePlayHours = 0;
    [ObservableProperty] private bool _isPlayHoursDetailVisible;
    [ObservableProperty] private bool _isGamesPlayedDetailVisible;
    [ObservableProperty] private bool _isActiveMonthDetailVisible;
    [ObservableProperty] private bool _isAverageHoursDetailVisible;
    [ObservableProperty] private ObservableCollection<MonthlyData>? _monthlyPlayHours;

    [ObservableProperty]
    private ISeries[]? _playHoursSeries;
    [ObservableProperty]
    private ISeries[]? _gamesPlayedSeries;
    [ObservableProperty]
    private ISeries[]? _activeMonthSeries;
    [ObservableProperty]
    private ISeries[]? _averageHoursSeries;
    
    [ObservableProperty]
    private IEnumerable<ICartesianAxis>? _playHoursXAxes;
    [ObservableProperty]
    private IEnumerable<ICartesianAxis>? _monthXAxes;
    [ObservableProperty]
    private IEnumerable<ICartesianAxis>? _averageHoursXAxes;

    private void InitializeCharts()
    {
        var months = new string[] { "1月", "2月", "3月", "4月", "5月", "6月", 
                                  "7月", "8月", "9月", "10月", "11月", "12月" };
        
        // 设置默认的图表样式
        SolidColorPaint defaultLabelStyle = new() { Color = SKColor.Parse("#303030"), };

        // 游玩时长柱状图
        PlayHoursSeries = new ISeries[]
        {
            new ColumnSeries<double>
            {
                Values = AnnualReportData.PlayedTimePerMonth,
                Name = "月度游玩时长",
                Fill = new SolidColorPaint(SKColor.Parse("#266489")),
                YToolTipLabelFormatter = point => $"{point.Model:F1} 小时",
            }
        };
        PlayHoursXAxes = new[]
        {
            new Axis
            {
                Labels = months,
                LabelsRotation = 0,
                LabelsPaint = defaultLabelStyle
            }
        };

        // 游戏完成度饼图
        List<ISeries> gamesPlayedSeries = new();
        foreach (KeyValuePair<PlayType, int> series in AnnualReportData.PlayTypeCnt)
        {
            if (series.Value == 0) continue;
            gamesPlayedSeries.Add(new PieSeries<double>
            {
                Values = [series.Value],
                Name = series.Key.GetLocalized(),
                // Fill = new SolidColorPaint(SKColor.Parse("#2c3e50")),
            });
        }
        GamesPlayedSeries = gamesPlayedSeries.ToArray();

        // 月度活跃度折线图
        var activity = new double[12];
        for (var i = 0; i < 12; i++)
        {
            activity[i] = AnnualReportData.PlayedTimePerMonth[i] + AnnualReportData.PlayedGamesPerMonth[i] * 10;
            MostActiveMonth = activity[i] > activity[MostActiveMonth] ? i : MostActiveMonth;
        }
        MostActiveMonth++; // 月份从1开始
        ActiveMonthSeries = new ISeries[]
        {
            new LineSeries<double>
            {
                Values = activity,
                Name = "月度活跃度",
                Fill = null,
                GeometrySize = 10,
                Stroke = new SolidColorPaint(SKColor.Parse("#68B9C0"), 3),
                GeometryStroke = new SolidColorPaint(SKColor.Parse("#68B9C0"), 3)
            }
        };
        
        MonthXAxes = new[]
        {
            new Axis
            {
                Labels = months,
                LabelsRotation = 0
            }
        };

        // 游戏时长分布条形图
        List<double> playedTimeRange = [];
        List<string> playedTimeRangeLabels = [];
        for(var i = 0; i < AnnualReportData.PlayedTimeRange.Length; i++)
            if (AnnualReportData.PlayedTimeRangeCnt[i] > 0)
            {
                playedTimeRange.Add(AnnualReportData.PlayedTimeRangeCnt[i]);
                var label = i == AnnualReportData.PlayedTimeRange.Length - 1
                    ? $"{AnnualReportData.PlayedTimeRange[i]}h+"
                    : $"{AnnualReportData.PlayedTimeRange[i]}-{AnnualReportData.PlayedTimeRange[i + 1]}h";
                playedTimeRangeLabels.Add(label);
            }
        AverageHoursSeries = new ISeries[]
        {
            new ColumnSeries<double>
            {
                Values = playedTimeRange.ToArray(),
                Name = "游戏数量",
                Fill = new SolidColorPaint(SKColor.Parse("#266489"))
            }
        };

        AverageHoursXAxes = new[]
        {
            new Axis
            {
                Labels = playedTimeRangeLabels.ToArray(),
                LabelsRotation = 0
            }
        };
    }

    public void OnNavigatedTo(object parameter)
    {
        Debug.Assert(parameter is AnnualReportData);
        AnnualReportData = (AnnualReportData)parameter;
        AveragePlayHours = AnnualReportData.PlayedTime / AnnualReportData.TotalGamesPlayed;
        // 初始化图表数据
        InitializeCharts();
    }

    public void OnNavigatedFrom()
    {

    }

    [RelayCommand]
    private void TogglePlayHoursDetail()
    {
        IsPlayHoursDetailVisible = !IsPlayHoursDetailVisible;

        IsGamesPlayedDetailVisible = false;
        IsActiveMonthDetailVisible = false;
        IsAverageHoursDetailVisible = false;
    }

    [RelayCommand]
    private void ToggleGamesPlayedDetail()
    {
        IsGamesPlayedDetailVisible = !IsGamesPlayedDetailVisible;

        IsPlayHoursDetailVisible = false;
        IsActiveMonthDetailVisible = false;
        IsAverageHoursDetailVisible = false;
    }

    [RelayCommand]
    private void ToggleActiveMonthDetail()
    {
        IsActiveMonthDetailVisible = !IsActiveMonthDetailVisible;

        IsPlayHoursDetailVisible = false;
        IsGamesPlayedDetailVisible = false;
        IsAverageHoursDetailVisible = false;
    }

    [RelayCommand]
    private void ToggleAverageHoursDetail()
    {
        IsAverageHoursDetailVisible = !IsAverageHoursDetailVisible;
        
        IsPlayHoursDetailVisible = false;
        IsGamesPlayedDetailVisible = false;
        IsActiveMonthDetailVisible = false;
    }
}

public class MonthlyData
{
    public string? Month { get; set; }
    public double Hours { get; set; }
}