using GalgameManager.Models.Sources;
using GalgameManager.WinApp.Base.Models.Filters;

namespace GalgameManager.Models.Filters;

public class SourceFilter(GalgameSourceBase source) : FilterBase
{
    public GalgameSourceBase Source { get; } = source;

    public override bool Apply(Galgame galgame) => Source.Contain(galgame);

    public override string Name { get; } = source.Name;
    protected override string SuggestName { get; } = $"{source.Name}/Source";
}