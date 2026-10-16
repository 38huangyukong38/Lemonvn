using Microsoft.UI.Xaml;

namespace GalgameManager.Views.GalgamePagePanel;

public partial class GameDescriptionPanel
{
    public GameDescriptionPanel()
    {
        InitializeComponent();
    }

    protected override void Update() =>
        Visibility = Game?.Description == null ? Visibility.Collapsed : Visibility.Visible;
}
