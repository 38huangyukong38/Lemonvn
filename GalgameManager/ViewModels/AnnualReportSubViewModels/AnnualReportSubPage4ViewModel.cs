using CommunityToolkit.Mvvm.ComponentModel;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Models;
using LiveChartsCore;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using System.Diagnostics;
using SkiaSharp;

namespace GalgameManager.ViewModels;

public partial class AnnualReportSubPage4ViewModel : ObservableObject, INavigationAware
{
    [ObservableProperty] private AnnualReportData _annualReportData = new();
    [ObservableProperty] private ISeries[]? _dayOfWeekSeries = Array.Empty<ISeries>();
    [ObservableProperty] private ICartesianAxis[]? _dayOfWeekXAxes = Array.Empty<ICartesianAxis>();

    public void OnNavigatedTo(object parameter)
    {
        Debug.Assert(parameter is AnnualReportData);
        AnnualReportData = (AnnualReportData)parameter;

        DayOfWeekSeries =
        [
            new ColumnSeries<double>
            {
                Values = AnnualReportData.PlayTimePerDayOfWeek,
                Name = "游玩时长(小时)",
                Fill = new SolidColorPaint(SKColors.CornflowerBlue)
            }
        ];

        DayOfWeekXAxes =
        [
            new Axis
            {
                Labels = ["周日", "周一", "周二", "周三", "周四", "周五", "周六"],
                LabelsRotation = 0,
                SeparatorsPaint = new SolidColorPaint(new SKColor(200, 200, 200)),
                SeparatorsAtCenter = false,
                TicksPaint = new SolidColorPaint(new SKColor(35, 35, 35)),
                TicksAtCenter = true
            }
        ];
    }

    public void OnNavigatedFrom()
    {
    }
}
