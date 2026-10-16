using System.Xml.Linq;
using LemonVN.Views.Anime;

namespace GalgameManager.Test.Services;

[TestFixture]
public sealed class LemonVisualLayoutTest
{
    [TestCase(1)]
    [TestCase(1.25)]
    [TestCase(1.5)]
    public void CardBottomGap_IsBoundedInPhysicalPixels(double scale)
    {
        double padding = AnimeCardLayoutMetrics.BottomPadding(scale);
        Assert.That(padding * scale, Is.EqualTo(3).Within(0.001));
        // Include the native item's one-DIP edge, not just the content padding.
        Assert.That((padding + 1) * scale, Is.InRange(3, 6));
        // WinUI's 1-DIP track is centered inside our 3-DIP ProgressBar control.
        double trailingTrackSpace = (3d - 1) / 2;
        Assert.That((padding + 1 + trailingTrackSpace) * scale, Is.InRange(3, 6));
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    public void InvalidDisplayScale_CannotProduceInvalidLayout(double scale) =>
        Assert.That(AnimeCardLayoutMetrics.BottomPadding(scale), Is.EqualTo(3));

    [Test]
    public void HomeCardsOptIntoCompactModeWithoutChangingLibrary()
    {
        var folder = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "GalgameManager.sln"))) folder = folder.Parent;
        Assert.That(folder, Is.Not.Null);
        XNamespace views = "using:LemonVN.Views.Anime";
        XDocument shelf = XDocument.Load(Path.Combine(folder!.FullName, "GalgameManager/Views/Anime/AnimeShelfControl.xaml"));
        XDocument library = XDocument.Load(Path.Combine(folder.FullName, "GalgameManager/Views/Anime/AnimeLibraryPage.xaml"));
        Assert.That(shelf.Descendants(views + "AnimeCardControl").Single().Attribute("IsCompact")?.Value, Is.EqualTo("True"));
        var libraryCards = library.Descendants(views + "AnimeCardControl").ToList();
        Assert.That(libraryCards, Is.Not.Empty);
        Assert.That(libraryCards.All(card => card.Attribute("IsCompact") is null), Is.True);
    }

    [Test]
    public void CompactRail_PreservesNativeNavigationRoutesAndAccessibleFooter()
    {
        var folder = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "GalgameManager.sln"))) folder = folder.Parent;
        Assert.That(folder, Is.Not.Null);
        XDocument shell = XDocument.Load(Path.Combine(folder!.FullName, "GalgameManager/Views/LemonVN/LemonShellPage.xaml"));
        XNamespace ns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace helpers = "using:GalgameManager.Helpers";
        XElement rail = shell.Root!.Element(ns + "NavigationView")!;
        Assert.That(rail.Attribute("PaneDisplayMode")!.Value, Is.EqualTo("Left"));
        Assert.That(double.Parse(rail.Attribute("OpenPaneLength")!.Value), Is.InRange(76, 96));
        Assert.That(rail.Attribute("IsBackButtonVisible")!.Value, Is.EqualTo("Visible"));
        Assert.That(rail.Element(ns + "NavigationView.PaneHeader"), Is.Null);
        var items = rail.Descendants(ns + "NavigationViewItem").ToList();
        Assert.That(items, Has.Count.EqualTo(5));
        Assert.That(items.Select(item => item.Attribute(helpers + "NavigationHelper.NavigateTo")!.Value), Is.Unique);
        foreach (XElement item in items)
        {
            Assert.That(item.Attribute("AutomationProperties.Name"), Is.Not.Null);
            Assert.That(item.Attribute("ToolTipService.ToolTip"), Is.Not.Null);
            XElement content = item.Element(ns + "StackPanel")!;
            Assert.That(content.Elements().Select(e => e.Name.LocalName), Is.EqualTo(new[] { "FontIcon", "TextBlock" }));
            Assert.That(content.Element(ns + "TextBlock")!.Attribute("TextWrapping")!.Value, Is.EqualTo("Wrap"));
        }
        XElement settings = rail.Element(ns + "NavigationView.FooterMenuItems")!.Element(ns + "NavigationViewItem")!;
        Assert.That(settings.Attribute(helpers + "NavigationHelper.NavigateTo")!.Value,
            Is.EqualTo("LemonVN.ViewModels.LemonSettingsViewModel"));
    }
}
