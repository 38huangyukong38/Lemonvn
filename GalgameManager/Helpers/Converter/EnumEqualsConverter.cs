using Microsoft.UI.Xaml.Data;
using System;

namespace GalgameManager.Helpers.Converter
{
    public class EnumEqualsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null || parameter == null)
                return false;

            string parameterString = parameter?.ToString() ?? string.Empty;
            
            // 直接比较字符串表示形式
            return value?.ToString()?.Equals(parameterString, StringComparison.Ordinal) ?? false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is bool boolValue && boolValue && parameter != null && targetType.IsEnum)
            {
                // 尝试将参数字符串解析为目标枚举类型
                string paramString = parameter.ToString() ?? string.Empty;
                if (!string.IsNullOrEmpty(paramString))
                {
                    return Enum.Parse(targetType, paramString);
                }
            }
            
            throw new NotImplementedException();
        }
    }
}