using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Models;
using GalgameManager.Services;
using GalgameManager.WinApp.Base.Models.Plugin;
using Moq;

namespace GalgameManager.Test;

[TestFixture]
public class SidebarServiceTest
{
    [Test]
    public async Task SaveVisibilityAsync_ShouldKeepSettingsButtonVisible()
    {
        Mock<ILocalSettingsService> settings = new();
        settings.Setup(x => x.ReadSettingAsync<Dictionary<string, bool>>(KeyValues.SidebarButtonVisibility, false, null, false))
            .ReturnsAsync(new Dictionary<string, bool> { [SidebarButtonIds.Settings] = false });
        settings.Setup(x => x.SaveSettingAsync(KeyValues.SidebarButtonVisibility,
                It.IsAny<Dictionary<string, bool>>(), false, false, null, false))
            .Returns(Task.CompletedTask);

        SidebarService service = new(settings.Object, Mock.Of<IInfoService>());

        await service.SaveVisibilityAsync(new Dictionary<string, bool> { [SidebarButtonIds.Home] = false });

        SidebarButton settingsButton = service.GetButtons().Single(b => b.UniqueId == SidebarButtonIds.Settings);
        Assert.That(settingsButton.IsVisible, Is.True);
    }

    [Test]
    public void RegisterPluginButton_ShouldExposePluginButton()
    {
        Mock<ILocalSettingsService> settings = new();
        settings.Setup(x => x.ReadSettingAsync<Dictionary<string, bool>>(KeyValues.SidebarButtonVisibility, false, null, false))
            .ReturnsAsync(new Dictionary<string, bool>());

        SidebarService service = new(settings.Object, Mock.Of<IInfoService>());
        Guid pluginId = Guid.NewGuid();

        service.RegisterPluginButton(pluginId, "Test Plugin", new SidebarButtonInfo
        {
            Id = "button",
            Text = "Plugin Button",
            Placement = SidebarButtonPlacement.Footer,
            FallbackGlyph = "\uE8A7",
        }, () => Task.CompletedTask);

        SidebarButton button = service.GetButtons().Single(b => b.UniqueId == SidebarButtonIds.CreatePluginButtonId(pluginId, "button"));
        Assert.Multiple(() =>
        {
            Assert.That(button.IsPlugin, Is.True);
            Assert.That(button.Title, Is.EqualTo("Plugin Button"));
            Assert.That(button.Placement, Is.EqualTo(SidebarButtonPlacement.Footer));
        });
    }
}
