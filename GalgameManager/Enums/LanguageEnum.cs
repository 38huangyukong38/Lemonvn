namespace GalgameManager.Enums;

public static class LanguageEnumExtensions
{
    /// <summary>
    /// 将 LanguageEnum 转换为 Steam API 使用的语言字符串
    /// </summary>
    /// <param name="language">语言枚举</param>
    public static string ToSteamApiString(this LanguageEnum language)
    {
        if (language == LanguageEnum.Auto)
        {
            IReadOnlyList<string>? userLanguages = Windows.System.UserProfile.GlobalizationPreferences.Languages;
            var primaryLanguage = userLanguages.FirstOrDefault(); // 获取第一个语言代码（例如 "zh-CN", "en-US"）
            if (!string.IsNullOrEmpty(primaryLanguage))
            {
                if (primaryLanguage.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                    return "schinese";
                if (primaryLanguage.StartsWith("ja", StringComparison.OrdinalIgnoreCase))
                    return "japanese";
                return "english";
            }
        }

        // 对于非 Auto 的情况，使用原有的逻辑
        return language switch
        {
            LanguageEnum.ChineseSimplified => "schinese",
            LanguageEnum.English => "english",
            LanguageEnum.Japanese => "japanese",
            _ => "english" // 默认或备选方案
        };
    }
}
