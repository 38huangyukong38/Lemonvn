using LemonVN.Views.Anime;

namespace GalgameManager.Test.Services;

[TestFixture]
public sealed class HorizontalPagingTest
{
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
