namespace GalgameManager.Enums;

// 注意：本Enum的每一个元素都需要添加Localize: PluginType_xxx 的本地化翻译
// 此外，还需要填写下面的Enum->图标
[Flags]
public enum PluginType
{
    All = 0,                //全部
    Official = 1 << 1,      //官方插件
    Parser = 1 << 2,        //搜刮器
    Huge = 1 << 3,          //大型插件
    Theme = 1 << 4,         //主题
    View = 1 << 5,          //界面优化
    Utility = 1 << 6,       //功能优化
    Library = 1 << 7,       //库
}

public static class PluginTypeHelper
{
    public static List<PluginType> GetAllTypes()
    {
        return Enum.GetValues(typeof(PluginType)).Cast<PluginType>().ToList();
    }

    public static List<PluginType> Separate(int pluginType) => ((PluginType)pluginType).Separate();

    public static List<PluginType> Separate(this PluginType pluginType)
    {
        return Enum.GetValues(typeof(PluginType)).Cast<PluginType>()
            .Where(type => type != PluginType.All && pluginType.HasFlag(type)).ToList();
    }

    public static string ToGlyph(this PluginType pluginType)
    {
        return pluginType switch
        {
            PluginType.All => "\uE80F",
            PluginType.Official => "\uEB95",
            PluginType.Parser => "\uEBD3",
            PluginType.Huge => "\uE82F",
            PluginType.Theme => "\uE790",
            PluginType.View => "\uEC0A",
            PluginType.Utility => "\uE90F",
            PluginType.Library => "\uE8F1",
            _ => "\uE8EF",
        };
    }
}
