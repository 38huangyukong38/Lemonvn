using GalgameManager.Models;
using Microsoft.UI.Xaml.Data;

namespace GalgameManager.Helpers.Converter;

public class StorePluginStatusToOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is StorePluginStatus status)
        {
            return status == StorePluginStatus.NotInstalled ? 0.0 : 1.0;
        }
        return 0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return null!;
    }
}

public class StorePluginStatusToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is StorePluginStatus status)
        {
            return status switch
            {
                StorePluginStatus.Installed => "StorePlugin_Installed".GetLocalized(),
                StorePluginStatus.UpdateAvailable => "StorePlugin_UpdateAvailable".GetLocalized(),
                _ => string.Empty
            };
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return null!;
    }
}

