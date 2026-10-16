using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;

namespace LemonVN.Helpers;

public static class LemonLocalization
{
    private static ResourceManager? _manager;
    private static ResourceContext? _context;

    // Configure both WinUI x:Uid and MRT Core code lookups before creating UI.
    // Explicit context avoids different language fallbacks in unpackaged builds.
    public static void Initialize()
    {
        string language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch
        {
            "zh" => "zh-CN",
            "ja" => "ja-JP",
            _ => "en-US"
        };
        Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = language;
        _manager = new ResourceManager(ResourceLoader.GetDefaultResourceFilePath());
        _context = _manager.CreateResourceContext();
        _context.QualifierValues["Language"] = language;
    }

    public static string GetString(string key, params object[] arguments)
    {
        if (_manager is null || _context is null) throw new InvalidOperationException("Localization is not initialized.");
        string value = _manager.MainResourceMap.GetSubtree("Resources").GetValue(key, _context).ValueAsString;
        return arguments.Length == 0 ? value : string.Format(CultureInfo.CurrentUICulture, value, arguments);
    }
}
