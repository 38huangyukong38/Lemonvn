using GalgameManager.Models.Sources;
using Microsoft.UI.Xaml.Data;

namespace GalgameManager.Helpers.Converter;
internal class SourceToGlyphConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not GalgameSourceBase source) return "\uE897"; //不应该发生
        switch (source.SourceType)
        {
            case GalgameSourceType.LocalFolder:
                if (value is not GalgameFolderSource folderSource) return "\uE8B7"; //不应该发生
                if (folderSource.NetworkDrive) return "\uE8CE";
                if (folderSource.RemoveableDrive) return "\uE88E";
                return "\uE8B7";
            case GalgameSourceType.LocalZip:
                return "\uF012";
            case GalgameSourceType.Virtual:
                return "\ue8ff";
            case GalgameSourceType.Steam:
                return "\uE7FC";
            default:
                return "\uE897";
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => GalgameSourceType.UnKnown; //不需要
}

internal class SourcesToStringConverter : IValueConverter
{
    private readonly SourceToGlyphConverter _sourceToGlyphConverter = new();
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if(value is not IEnumerable<GalgameSourceBase> sources) return string.Empty;
        IEnumerable<string> tmp = sources.Select(s =>
            _sourceToGlyphConverter.Convert(s, targetType, parameter, language) as string ??
            string.Empty);
        return string.Join(" ", tmp);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => null!; //不需要
}

internal class SourceToDescriptionStrConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not GalgameSourceBase source) return string.Empty;
        switch (source.SourceType)
        {
            case GalgameSourceType.LocalFolder:
                if (value is not GalgameFolderSource folderSource) return string.Empty; //不应该发生
                if (folderSource.NetworkDrive) return "GalgameSourceType_LocalFolder_Net".GetLocalized();
                if (folderSource.RemoveableDrive) return "GalgameSourceType_LocalFolder_Removeable".GetLocalized();
                return "GalgameSourceType_LocalFolder".GetLocalized();
            default:
                return source.SourceType.GetLocalized();
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => null!; //不需要
}