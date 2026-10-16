using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace GalgameManager.Helpers;

public static class BoolExtensions
{
    public static Visibility ToVisibility(this bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility Reverse(this Visibility value) =>
        value == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
}


public class SortKeyToBooleanConverter : IValueConverter
{
    // 判断当前的 Enum 值是否等于 ConverterParameter 传入的字符串
    public object Convert(object? value, Type targetType, object? parameter, string language)
    {
        if (value == null || parameter == null) return false;
        var checkValue = value.ToString()!;
        var targetValue = parameter.ToString()!;
        return checkValue.Equals(targetValue, StringComparison.OrdinalIgnoreCase);
    }

    // 当 RadioButton 被选中(True)时，将 Parameter 字符串转回 Enum
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
