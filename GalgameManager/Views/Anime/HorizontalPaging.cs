namespace LemonVN.Views.Anime;

public static class HorizontalPaging
{
    public static HorizontalPage GetPage(int total, double viewport, double stride, int requestedPage)
    {
        total = Math.Max(0, total);
        double visible = double.IsFinite(viewport) && viewport > 0 && double.IsFinite(stride) && stride > 0
            ? Math.Max(1, Math.Floor(viewport / stride)) : 1;
        int size = (int)Math.Min(Math.Max(1, total), visible);
        int pages = total == 0 ? 0 : (total - 1) / size + 1;
        int index = Math.Clamp(requestedPage, 0, Math.Max(0, pages - 1));
        int start = index * size;
        return new(start, Math.Min(size, total - start), index, pages);
    }

    public static double NextOffset(double offset, double viewport, double scrollable, double itemStride, bool forward)
    {
        if (!double.IsFinite(offset) || !double.IsFinite(viewport) || !double.IsFinite(scrollable) ||
            !double.IsFinite(itemStride) || viewport <= 0 || scrollable <= 0 || itemStride <= 0) return 0;
        double step = Math.Max(1, Math.Floor(viewport / itemStride)) * itemStride;
        return Math.Clamp(offset + (forward ? step : -step), 0, scrollable);
    }
}

public readonly record struct HorizontalPage(int Start, int Count, int Index, int Pages);
