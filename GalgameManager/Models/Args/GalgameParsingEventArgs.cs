namespace GalgameManager.Models;

/// <summary>
///【非UI线程触发】 <br/>
/// 当某游戏搜刮进度变化时触发
/// </summary>
/// <param name="galgame"></param>
/// <param name="message"></param>
public class GalgameParsingEventArgs(Galgame galgame, string message)
{
    public string Message { get; } = message;
    public Galgame Galgame { get; } = galgame;
}