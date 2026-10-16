using System.Text.RegularExpressions;

namespace GalgameManager.Helpers;

public static class NameRegex
{
    /// <summary>
    /// 使用正则表达式获取游戏名
    /// </summary>
    /// <param name="targetString">待匹配串</param>
    /// <param name="pattern">正则匹配串</param>
    /// <param name="removeBorder">是否要移除所得子串的边界</param>
    /// <param name="index">要第几个子串</param>
    /// <returns></returns>
    public static string GetName(string targetString, string pattern, bool removeBorder, int index)
    {
        var result = string.Empty;
        try
        {
            Regex regex = new(pattern);
            MatchCollection match = regex.Matches(targetString);
            if (match.Count > index)
            {
                result = match[index].Value;
                if (removeBorder && result.Length >= 2)
                    result = result.Substring(1, result.Length - 2);
            }
        }
        catch (Exception)
        {
            // 如果正则表达式或字符串操作出错，返回原始字符串
            result = targetString;
        }
        return result;
    }
}
