using System.Xml.Linq;
using LemonVN.Views.Anime;

namespace GalgameManager.Test.Services;

[TestFixture]
public sealed class LemonVisualLayoutTest
{
    [Test]
    public void BrowseArrowsAndFilterSubmission_RetainNativeVirtualizationAndExplicitKeyboardAction()
    {
        var folder = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "GalgameManager.sln"))) folder = folder.Parent;
        Assert.That(folder, Is.Not.Null);
        XNamespace ns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace views = "using:LemonVN.Views.Anime";
        foreach (string name in new[] { "Views/Anime/AnimeShelfControl.xaml", "Views/LemonVN/LemonHomePage.xaml" })
        {
            XDocument page = XDocument.Load(Path.Combine(folder!.FullName, "GalgameManager", name));
            Assert.That(page.Descendants(views + "HorizontalPager"), Has.Exactly(1).Items);
            XElement list = page.Descendants(ns + "ListView").Single();
            Assert.That(list.Attribute("ScrollViewer.HorizontalScrollBarVisibility")?.Value, Is.EqualTo("Hidden"));
            Assert.That(list.Descendants(ns + "ItemsStackPanel"), Has.Exactly(1).Items);
        }
        XDocument library = XDocument.Load(Path.Combine(folder!.FullName, "GalgameManager/Views/Anime/AnimeLibraryPage.xaml"));
        XElement apply = library.Descendants(ns + "Button").Single(e => e.Attribute("AutomationProperties.AutomationId")?.Value == "AnimeApplyFilters");
        Assert.That(apply.Attribute("Click")?.Value, Is.EqualTo("OnApplyFilters"));
        Assert.That(apply.Ancestors(ns + "StackPanel").Any(e => e.Attribute("PreviewKeyDown")?.Value == "OnFilterKeyDown"), Is.True);
        XElement panel = apply.Ancestors(ns + "StackPanel").First();
        Assert.That(panel.Descendants(ns + "ComboBox"), Is.Empty, "Filter selection must not open a second dismissible popup.");
        Assert.That(panel.Descendants(ns + "RadioButton"), Has.Exactly(6).Items);
        XElement tags = panel.Element(ns + "ListView")!;
        Assert.That(double.Parse(tags.Attribute("MaxHeight")!.Value, System.Globalization.CultureInfo.InvariantCulture), Is.LessThanOrEqualTo(160));
        Assert.That(panel.Parent!.Attribute("Opening")?.Value, Is.EqualTo("OnFilterOpening"));
        Assert.That(panel.Parent.Attribute("Opened")?.Value, Is.EqualTo("OnFilterOpened"));
        Assert.That(panel.Parent.Attribute("ShowMode")?.Value, Is.EqualTo("Standard"));
        Assert.That(panel.Parent.Attribute("AllowFocusOnInteraction")?.Value, Is.EqualTo("True"));
    }
    [TestCase("https://lain.bgm.tv/pic/cover/l/a.jpg", false)]
    [TestCase("file://server/share/cover.jpg", false)]
    [TestCase("file:///C:/synthetic/cover.jpg?other=1", false)]
    [TestCase("ms-appx:///Assets/LemonPlaceholder.png", false)]
    [TestCase("file:///C:/synthetic%20data/%E5%B0%81%E9%9D%A2.jpg", true)]
    public void LocalCoverRecovery_NeverFallsBackToNetworkOrUnc(string source, bool allowed) =>
        Assert.That(LocalCoverImagePolicy.CanReadLocalUri(new Uri(source)), Is.EqualTo(allowed));
    [Test]
    public void BangumiConfirmation_IsSingleSharedHeaderAction_OutsideScrollableMetadata()
    {
        var folder = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "GalgameManager.sln"))) folder = folder.Parent;
        Assert.That(folder, Is.Not.Null);
        XNamespace ns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XDocument page = XDocument.Load(Path.Combine(folder!.FullName, "GalgameManager/Views/Anime/AnimeLookupPage.xaml"));
        XElement button = page.Descendants(ns + "Button").Single(element => element.Attribute("AutomationProperties.AutomationId")?.Value == "BangumiConfirm");
        Assert.Multiple(() =>
        {
            Assert.That(button.Ancestors(ns + "ScrollViewer"), Is.Empty);
            Assert.That(button.Attribute("Grid.Column")?.Value, Is.EqualTo("1"));
            Assert.That(button.Attribute("Command")?.Value, Does.Contain("ViewModel.ConfirmCommand"));
            Assert.That(button.Attribute("IsEnabled")?.Value, Does.Contain("ViewModel.CanConfirm"));
            Assert.That(button.Parent!.Elements(ns + "TextBlock").Single().Attribute(XName.Get("Uid", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value, Is.EqualTo("Bangumi_Preview"));
        });
        XElement scroll = page.Descendants(ns + "ScrollViewer").Single(element => element.Attribute("AutomationProperties.AutomationId")?.Value == "BangumiPreviewScroll");
        Assert.That(scroll.Attribute("Grid.Row")?.Value, Is.EqualTo("1"));
    }
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
