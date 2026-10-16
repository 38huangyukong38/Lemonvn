using LiteDB;

namespace GalgameManager.Models;

/// <summary>
/// 注意：添加ScanResultType种类时，请确保在GalgameManager/Helpers/Converter/GlyphConverters/ScanResultToGlyph.cs和ScanResultToBrush.cs中也添加相应的转换逻辑。<br/>
/// 同时需要注意添加相关的翻译条目，如：“ScanResultType_Success -> "游戏已成功添加"”
/// </summary>
public enum ScanResultType
{
    Information, // For general messages, like scan start time or non-error specific logs
    Success,
    AlreadyExists,
    RequiresConfirmation, // 仅名称匹配到已有游戏，等待用户确认关联
    Failed
}

public class PathScanResultItem
{
    public string Path { get; set; } = string.Empty;
    public ScanResultType ResultType { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid? RelatedGameId { get; set; }
    [BsonIgnore] public bool RequiresConfirmation =>
        ResultType == ScanResultType.RequiresConfirmation; // 是否等待用户确认关联
}

public class GalgameScanResult
{
    [BsonId]
    public Guid SourceId { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public DateTime ScanTime { get; set; }
    public List<PathScanResultItem> Results { get; set; } = new();
}
