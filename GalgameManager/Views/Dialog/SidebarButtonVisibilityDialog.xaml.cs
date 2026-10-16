using System.Collections.ObjectModel;
using GalgameManager.Contracts.Services;
using GalgameManager.Models;
using GalgameManager.WinApp.Base.Models.Plugin;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views.Dialog;

public sealed partial class SidebarButtonVisibilityDialog : ContentDialog
{
    private readonly ISidebarService _sidebarService = App.GetService<ISidebarService>();
    public ObservableCollection<SidebarBtnSettingItem> MenuButtons { get; } = [];
    public ObservableCollection<SidebarBtnSettingItem> FooterButtons { get; } = [];

    public SidebarButtonVisibilityDialog(IEnumerable<SidebarButton> buttons)
    {
        InitializeComponent();
        XamlRoot = App.MainWindow!.Content.XamlRoot;

        foreach (SidebarButton button in buttons.OrderBy(b => b.Order))
        {
            SidebarBtnSettingItem item = new()
            {
                UniqueId = button.UniqueId,
                Title = button.Title,
                Description = button.Description,
                Placement = button.Placement,
                Order = button.Order,
                CanToggle = button.UniqueId != SidebarButtonIds.Settings,
                IsVisible = button.IsVisible,
            };

            if (button.Placement == SidebarButtonPlacement.Menu)
                MenuButtons.Add(item);
            else
                FooterButtons.Add(item);
        }
    }

    public Dictionary<string, bool> GetVisibilityMap()
    {
        return MenuButtons.Concat(FooterButtons)
            .Where(item => item.UniqueId != SidebarButtonIds.Settings)
            .ToDictionary(item => item.UniqueId, item => item.IsVisible);
    }

    private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            await _sidebarService.SaveVisibilityAsync(GetVisibilityMap());
        }
        finally
        {
            deferral.Complete();
        }
    }
}
