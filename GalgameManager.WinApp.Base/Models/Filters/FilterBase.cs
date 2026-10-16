using GalgameManager.Contracts;
using GalgameManager.Models;
using GalgameManager.WinApp.Base.Contracts;

namespace GalgameManager.WinApp.Base.Models.Filters;

public abstract class FilterBase : IFilter
{
    public abstract bool Apply(Galgame galgame);

    public abstract string Name { get; }

    /// <summary>
    /// 在添加过滤器时 AutoSuggestBox 会显示的内容
    /// </summary>
    protected abstract string SuggestName { get; }

    public bool Revert { get; set; }

    public override string ToString() => SuggestName;
}
