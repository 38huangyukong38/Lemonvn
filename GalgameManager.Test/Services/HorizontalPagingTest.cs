using LemonVN.Views.Anime;

namespace GalgameManager.Test.Services;

[TestFixture]
public sealed class HorizontalPagingTest
{
    [TestCase(9, 1128, 156, 0, 0, 7, 0, 2)]
    [TestCase(9, 1128, 156, 1, 7, 2, 1, 2)]
    [TestCase(9, 1128, 156, 99, 7, 2, 1, 2)]
    [TestCase(9, 1128, 156, -1, 0, 7, 0, 2)]
    [TestCase(11, 1128, 202, 1, 5, 5, 1, 3)]
    [TestCase(11, 1128, 202, 2, 10, 1, 2, 3)]
    [TestCase(0, 1128, 156, 1, 0, 0, 0, 0)]
    [TestCase(1, 1128, 156, 0, 0, 1, 0, 1)]
    [TestCase(1000, 600, 156, 1, 3, 3, 1, 334)]
    public void PageProjection_OnlyIncludesCurrentPage_AndClampsNavigation(int total, double width, double stride,
        int index, int start, int count, int expectedIndex, int pages) =>
        Assert.That(HorizontalPaging.GetPage(total, width, stride, index), Is.EqualTo(new HorizontalPage(start, count, expectedIndex, pages)));

    [TestCase(0)]
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    public void InvalidViewport_StillProducesABoundedPage(double width) =>
        Assert.That(HorizontalPaging.GetPage(10, width, 156, 99), Is.EqualTo(new HorizontalPage(9, 1, 9, 10)));

    [TestCase(0, 1000, 1500, 156, true, 936)]
    [TestCase(936, 1000, 1500, 156, true, 1500)]
    [TestCase(1500, 1000, 1500, 156, true, 1500)]
    [TestCase(936, 1000, 1500, 156, false, 0)]
    [TestCase(1500, 500, 1500, 202, false, 1096)]
    [TestCase(0, 100, 500, 156, true, 156)]
    [TestCase(0, 1000, 0, 156, true, 0)]
    [TestCase(0, 0, 500, 156, true, 0)]
    public void Paging_MovesByWholeVisibleItems_AndClampsBothEnds(double offset, double viewport, double extent,
        double stride, bool forward, double expected) =>
        Assert.That(HorizontalPaging.NextOffset(offset, viewport, extent, stride, forward), Is.EqualTo(expected));
}
