using Windows.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;

namespace GalgameManager.Helpers;

public static class ResourceExtensions
{
    private static readonly ResourceLoader _resourceLoader = new();

    public static string GetLocalized(this string resourceKey)
    {
        try
        {
            return _resourceLoader.GetString(resourceKey);
        }
        catch (Exception)
        {
            return resourceKey;
        }
    }

    public static string GetLocalized(this string resourceKey, params object[] args)
    {
        try
        {
            return string.Format(resourceKey.GetLocalized(), args);
        }
        catch (Exception)
        {
            return resourceKey;
        }
    }

    public static string GetLocal()
    {
        var currentLanguage = ApplicationLanguages.PrimaryLanguageOverride;
        if (string.IsNullOrEmpty(currentLanguage))
        {
            IReadOnlyList<string>? languages = ApplicationLanguages.Languages;
            if (languages.Count > 0)
            {
                currentLanguage = languages[0];
            }
        }

        //修改为xx-xx格式
        List<string> tmp = currentLanguage.Split('-').ToList();
        if (tmp.Count == 3)
            currentLanguage = tmp[0] + "-" + tmp[2];

        return currentLanguage;
    }
}
