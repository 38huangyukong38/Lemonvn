namespace LemonVN.Views.Anime;

// View-only dimensions. Data, query rules and progress calculations do not depend on them.
public static class AnimeCardLayoutMetrics
{
    // Three physical pixels plus the item's 1 DIP focus/selection edge stays within
    // 3-6 physical pixels at the supported 100%, 125% and 150% display scales.
    public static double BottomPadding(double rasterizationScale) =>
        3 / (double.IsFinite(rasterizationScale) && rasterizationScale > 0 ? rasterizationScale : 1);
}
