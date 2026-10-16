using GalgameManager.Helpers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using DependencyPropertyGenerator;

namespace GalgameManager.Views.Control;

[DependencyProperty<string>("FluentGlyph")]
[DependencyProperty<Symbol>("Symbol", DefaultValue = Symbol.Accept)]
[DependencyProperty<double>("IconFontSize", DefaultValue = double.NaN)]
public partial class ConditionalFontIcon : FontIcon
{
    private static bool? _isSegoeFluentIconsInstalled;

    public ConditionalFontIcon()
    {
        UpdateIcon();
    }

    partial void OnIconFontSizeChanged() => UpdateIcon();
    partial void OnFluentGlyphChanged() => UpdateIcon();
    partial void OnSymbolChanged() => UpdateIcon();

    private void UpdateIcon()
    {
        _isSegoeFluentIconsInstalled ??= Utils.IsFontInstalled("Segoe Fluent Icons");
        if (_isSegoeFluentIconsInstalled is true)
        {
            FontFamily = new FontFamily("Segoe Fluent Icons");
            Glyph = FluentGlyph;
        }
        else
        {
            FontFamily = new FontFamily("Segoe MDL2 Assets");
            Glyph = ((char)Symbol).ToString();
        }

        if (!double.IsNaN(IconFontSize))
            FontSize = IconFontSize;
        else
            ClearValue(FontSizeProperty); // 继承父元素的 FontSize
    }
}