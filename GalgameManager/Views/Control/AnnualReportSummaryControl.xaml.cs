using GalgameManager.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views.Control;

public sealed partial class AnnualReportSummaryControl : UserControl
{
    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register(nameof(Data), typeof(AnnualReportData), typeof(AnnualReportSummaryControl), new PropertyMetadata(null));

    public AnnualReportData Data
    {
        get => (AnnualReportData)GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public AnnualReportSummaryControl()
    {
        this.InitializeComponent();
    }
}
