using GalgameManager.Models.Sources;

namespace GalgameManager.WinApp.Base.Contracts.NavigationApi.NavigateParameters;

public class LibraryPageNavParameter
{
    /// <summary>
    /// 进入该界面后直接打开某个游戏库(而不用用户一层一层点进去)
    /// </summary>
    public GalgameSourceBase? TargetSource;
}
