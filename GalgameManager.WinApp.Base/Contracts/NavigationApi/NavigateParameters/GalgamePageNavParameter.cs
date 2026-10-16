using GalgameManager.Models;

namespace GalgameManager.WinApp.Base.Contracts.NavigationApi.NavigateParameters;

public class GalgamePageNavParameter
{
    /// 目标游戏
    public required Galgame Galgame;
    /// 进入游戏详情界面后是否启动游戏
    public bool StartGame;
}

