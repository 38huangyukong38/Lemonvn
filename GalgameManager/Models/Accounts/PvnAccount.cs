namespace GalgameManager.Models;

public class PvnAccount
{
    public required string Token;
    public required int Id;
    public required string UserName;
    public required string UserDisplayName;
    public string? Avatar;
    public required long ExpireTimestamp;
    public long RefreshTimestamp => ExpireTimestamp - 60 * 60 * 24 * 10; // 提前10天刷新
    public required LoginMethodEnum LoginMethod;
    public required long TotalSpace;
    public required long UsedSpace;
    
    public enum LoginMethodEnum
    {
        Default,
        Bangumi
    }
}