namespace LemonVN.Views.Anime;

public static class HorizontalPaging
{
    public static double NextOffset(double offset, double viewport, double scrollable, double itemStride, bool forward)
    {
        if (!double.IsFinite(offset) || !double.IsFinite(viewport) || !double.IsFinite(scrollable) ||
            !double.IsFinite(itemStride) || viewport <= 0 || scrollable <= 0 || itemStride <= 0) return 0;
        double step = Math.Max(1, Math.Floor(viewport / itemStride)) * itemStride;
        return Math.Clamp(offset + (forward ? step : -step), 0, scrollable);
    }
}
