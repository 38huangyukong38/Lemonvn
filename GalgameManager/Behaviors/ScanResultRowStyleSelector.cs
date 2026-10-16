using GalgameManager.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Behaviors;

public class ScanResultRowStyleSelector : StyleSelector
{
    public Style? InfoStyle { get; set; }
    public Style? SuccessStyle { get; set; }
    public Style? WarningStyle { get; set; } // For AlreadyExists
    public Style? ErrorStyle { get; set; }

    protected override Style SelectStyleCore(object item, DependencyObject container)
    {
        if (item is PathScanResultItem resultItem)
        {
            return resultItem.ResultType switch
            {
                ScanResultType.Success => SuccessStyle ?? base.SelectStyleCore(item, container),
                ScanResultType.AlreadyExists => WarningStyle ?? base.SelectStyleCore(item, container),
                ScanResultType.Failed => ErrorStyle ?? base.SelectStyleCore(item, container),
                ScanResultType.Information => InfoStyle ?? base.SelectStyleCore(item, container),
                _ => base.SelectStyleCore(item, container)
            };
        }
        return base.SelectStyleCore(item, container);
    }
}
