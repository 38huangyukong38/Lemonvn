using GalgameManager.Enums;
using GalgameManager.Models;

namespace GalgameManager.Contracts.Services;

public interface IStaffService
{
    /// <summary>
    /// 当某个游戏的staff列表发生变化时触发 (UI线程触发)
    /// </summary>
    public event Action<Galgame> OnGameStaffChanged;
    public event Action<Staff> OnStaffSaved;
    public event Action<Staff> OnStaffDeleted;
    
    public Task InitAsync();
    
    public Staff? GetStaff(Guid? id);
    
    /// <summary>
    /// 返回相似度最高的staff，如果相似度全为0则返回null<br/>
    /// 相似度计算方法见<see cref="StaffIdentifier.Match"/>
    /// </summary>
    /// <param name="identifier"></param>
    /// <returns></returns>
    public Staff? GetStaff(StaffIdentifier identifier);
    
    /// <summary>
    /// 获取某个galgame的staff列表
    /// </summary>
    /// <param name="game"></param>
    /// <returns></returns>
    public List<Staff> GetStaffs(Galgame game);

    /// <summary>
    /// 获取所有staff列表
    /// </summary>
    /// <returns></returns>
    public List<Staff> GetStaffs();

    /// <summary>
    /// 搜刮staff信息，直接修改传入的staff对象
    /// </summary>
    /// <param name="staff"></param>
    /// <param name="rss">信息源</param>
    /// <returns></returns>
    public Task<Staff> ParseStaffAsync(Staff staff, RssType rss);

    /// <summary>
    /// 搜刮某个游戏的staffs
    /// </summary>
    /// <param name="game"></param>
    /// <returns></returns>
    public Task ParseStaffAsync(Galgame game);
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="staff"></param>
    /// <param name="sync">是否同步到pvn云端</param>
    public void Save(Staff staff, bool sync = true);
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="staff"></param>
    /// <param name="sync">是否同步到pvn云端</param>
    public void Delete(Staff staff, bool sync = true);
    
    public Task ExportAsync(Action<string, int, int>? progress);
}