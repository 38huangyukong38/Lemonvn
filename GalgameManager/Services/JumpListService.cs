using Windows.UI.StartScreen;

using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Services;

/// <summary>
/// JumpList 管理
/// "/j galgamePath"
/// </summary>
public class JumpListService (IInfoService infoService)  : IJumpListService
{
    private JumpList? _jumpList;
    private const int MaxItems = 5;

    private async Task Init()
    {
        _jumpList = await JumpList.LoadCurrentAsync();
        _jumpList.SystemGroupKind = JumpListSystemGroupKind.None;
    }

    public async Task CheckJumpListAsync(IList<Galgame> galgames)
    {
        if (!RuntimeHelper.IsMSIX) return;
        try
        {
            if (_jumpList == null) await Init();
            List<JumpListItem> toRemove = _jumpList!.Items.Where(item => galgames.All(gal => $"/j \"{gal.Uuid}\"" != item.Arguments)).ToList();
            foreach (JumpListItem item in toRemove)
            {
                _jumpList.Items.Remove(item);
            }
            await _jumpList!.SaveAsync();
        }
        catch (Exception e)
        {
            infoService.Event(EventType.NotCriticalUnexpectedError, InfoBarSeverity.Error, "检查JumpList失败", e);
        }
    }

    public async Task AddToJumpListAsync(Galgame galgame)
    {
        if (!RuntimeHelper.IsMSIX) return;
        try
        {
            if (_jumpList == null) await Init();
            IList<JumpListItem>? items = _jumpList!.Items;
            JumpListItem? item = items.FirstOrDefault(i => i.Arguments == $"/j \"{galgame.Uuid}\"");
            if (item == null)
            {
                item = JumpListItem.CreateWithArguments($"/j \"{galgame.Uuid}\"", galgame.Name);
                item.Logo = new Uri("ms-appx:///Assets/heart.png");
            }
            else
                items.Remove(item);
            items.Insert(0, item);
            if (items.Count > MaxItems)
                items.RemoveAt(items.Count-1);
            await _jumpList!.SaveAsync();
        }
        catch (Exception e)
        {
            infoService.Event(EventType.NotCriticalUnexpectedError, InfoBarSeverity.Error, "添加JumpList失败", e);
        }
    }

    public async Task RemoveFromJumpListAsync(Galgame galgame)
    {
        if (!RuntimeHelper.IsMSIX) return;
        try
        {
            if (_jumpList == null) await Init();
            IList<JumpListItem>? items = _jumpList!.Items;
            JumpListItem? item = items.FirstOrDefault(i => i.Arguments == $"/j \"{galgame.Uuid}\"");
            if (item != null)
            {
                items.Remove(item);
                await _jumpList!.SaveAsync();
            }
        }
        catch (Exception e)
        {
            infoService.Event(EventType.NotCriticalUnexpectedError, InfoBarSeverity.Error, "移除JumpList失败", e);
        }
    }
}
