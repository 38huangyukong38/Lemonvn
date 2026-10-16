using GalgameManager.WinApp.Base.Models.Filters;

namespace GalgameManager.Models.Filters;

public class StaffFilter (Staff staff) : FilterBase
{
    public override bool Apply(Galgame galgame) => staff.Games.Any(g => g.Game == galgame);

    public override string Name => staff.Name ?? string.Empty;
    
    protected override string SuggestName => $"{staff.Name}/Staff";
}