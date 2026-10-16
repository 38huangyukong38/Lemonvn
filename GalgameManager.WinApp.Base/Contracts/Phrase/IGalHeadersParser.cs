using System.Collections.Generic;
using System.Threading.Tasks;
using GalgameManager.Models;

namespace GalgameManager.Contracts.Phrase;

public interface IGalHeadersParser
{
    /// <summary>
    /// 从rss中获取galgame的所有可能的Header图片
    /// </summary>
    /// <param name="galgame">galgame</param>
    /// <returns>所有可能的Header图片url列表，如果没有则返回空列表</returns>
    Task<List<string>> GetGalHeadersAsync(Galgame galgame);
}
