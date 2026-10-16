using GalgameManager.Contracts;
using GalgameManager.Contracts.Phrase;
using GalgameManager.Enums;
using GalgameManager.Models;
using GalgameManager.Models.Sources;
using GalgameManager.WinApp.Base.Contracts;
using GalgameManager.WinApp.Base.Contracts.Dialogs;
using GalgameManager.WinApp.Base.Contracts.PluginUi;
using GalgameManager.WinApp.Base.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Services;

/// <summary>
/// 一个实现了所有插件扩展接口的插件模板，用来给IDE正确静态分析
/// </summary>
public class PluginTemplate :
    IPlugin,
    IHttpClientProvider,
    IParserProvider,
    ISourceProvider,
    IPluginAccount,
    IPluginSetting,
    IGalgamePage,
    IGalgamePageLeftPanel,
    IGalgamePageRightPanel,
    IGalgamePageSetting,
    IGalInfoPhraser,
    IGalHeadersParser,
    IGalCoversParser,
    IGalStaffParser,
    IGalCharacterPhraser,
    IDisplayableGameObject
{
    private static readonly PluginInfo TemplateInfo = new()
    {
        Id = Guid.Parse("8f0c2dc8-3bb5-4b65-aecf-1c6d2b116f74"),
        Name = "PluginTemplate",
        Description = "Template plugin that implements all plugin extension interfaces (stub).",
    };

    public PluginInfo Info => TemplateInfo;

    public Task InitializeAsync(IPotatoVnApi hostApi) => Task.CompletedTask;

    public HttpClient? HttpClient => null;

    public string ParserName => "TemplateParser";

    public IGalInfoPhraser GetPhraser() => this;

    public GalgameSourceType SourceType => (GalgameSourceType)101;

    public string SourceTypeName => "TemplateSource";

    public UIElement GetAddSourceDialogContent(IAddSourceDialog dialog)
    {
        return new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = "PluginTemplate - Add Source (stub)" },
                new TextBlock { Text = "This UI is a placeholder for static analysis." }
            }
        };
    }

    public FrameworkElement CreateAccountUi()
    {
        return new TextBlock { Text = "PluginTemplate Account UI (stub)" };
    }

    public FrameworkElement CreateSettingUi()
    {
        return new TextBlock { Text = "PluginTemplate Setting UI (stub)" };
    }

    public Task SettingAsync() => Task.CompletedTask;

    public Task<Galgame?> GetGalgameInfo(Galgame galgame) => Task.FromResult<Galgame?>(null);

    public RssType GetPhraseType() => RssType.None;

    public Task<List<string>> GetGalHeadersAsync(Galgame galgame) => Task.FromResult(new List<string>());

    public Task<List<string>> GetGalCoversAsync(Galgame galgame) => Task.FromResult(new List<string>());

    public Task<Staff?> GetStaffAsync(Staff staff) => Task.FromResult<Staff?>(null);

    public Task<List<StaffRelation>> GetStaffsAsync(Galgame game) => Task.FromResult(new List<StaffRelation>());

    public Task<GalgameCharacter?> GetGalgameCharacter(GalgameCharacter galgameCharacter)
        => Task.FromResult<GalgameCharacter?>(null);

    public Task<FrameworkElement> CreateUiAsync(Galgame game) => null!;

    public Task<FrameworkElement> CreateLeftPanelUiAsync(Galgame game) => null!;

    public Task<FrameworkElement> CreateRightPanelUiAsync(Galgame game) => null!;
}
