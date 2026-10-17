// LemonVN modification from the fixed PotatoVN baseline; see LICENSE, NOTICE and UPSTREAM_PROVENANCE.json.
using GalgameManager.Helpers;

namespace GalgameManager;

// LemonVN adaptation: reuse WindowEx with independent window branding.
public sealed partial class MainWindow : WindowEx
{
    public MainWindow()
    {
        InitializeComponent();

        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets/LemonAppIcon.ico"));
        Content = null;
        Title = "AppDisplayName".GetLocalized();
    }
}
