using Microsoft.UI.Xaml.Data;

namespace GalgameManager.Helpers.Converter;

public class DoubleToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is double d)
        {
            return d.ToString(parameter as string ?? "F1");
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => null!; // Not needed
}
