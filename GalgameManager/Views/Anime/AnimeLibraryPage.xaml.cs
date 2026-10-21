using GalgameManager;
using LemonVN.Application.Anime;
using LemonVN.Domain.Anime;
using LemonVN.Helpers;
using LemonVN.ViewModels.Anime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace LemonVN.Views.Anime;

public sealed partial class AnimeLibraryPage : Page
{
    public AnimeLibraryViewModel ViewModel { get; }
    public AnimeLibraryPage()
    {
        ViewModel = App.GetService<AnimeLibraryViewModel>();
        InitializeComponent();
    }

    private void OnAnimeClicked(object sender, ItemClickEventArgs args) => ViewModel.OpenAnimeCommand.Execute(args.ClickedItem);

    private void OnFilterOpening(object sender, object args) => ViewModel.BeginFilterEdit();
    private void OnFilterOpened(object sender, object args)
    {
        // CommandBar pointer invocation may leave focus on the navigation rail.
        // Put keyboard input inside this popup before handling its Enter action.
        ApplyFilterButton.Focus(FocusState.Programmatic);
    }

    private void OnApplyFilters(object sender, RoutedEventArgs args) => ApplySelectedFilters();
    private void OnFilterKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key != VirtualKey.Enter || args.KeyStatus.WasKeyDown) return;
        args.Handled = true;
        ApplySelectedFilters();
    }
    private void ApplySelectedFilters()
    {
        // Keep selection changes out of the active list until the whole popup is submitted.
        ViewModel.DraftStatusIndex = Math.Max(0, StatusFilter.SelectedIndex);
        ViewModel.DraftSelectedTag = TagFilter.SelectedItem as string;
        ViewModel.ApplyFiltersCommand.Execute(null);
        FilterFlyout.Hide();
    }

    // Platform menu construction only; persistence/confirmation stays in the ViewModel and existing services.
    private void OnCardMenuOpening(object sender, object args)
    {
        var menu = (MenuFlyout)sender;
        menu.Items.Clear();
        AnimeCardViewModel? card = menu.Target is GridViewItem container
            ? container.Content as AnimeCardViewModel : menu.Target?.DataContext as AnimeCardViewModel;
        if (card is null) return;
        AnimeCardTarget target = card.Target;
        var status = new MenuFlyoutSubItem { Text = Text("Ux_ContextStatus"), Icon = new SymbolIcon(Symbol.AllApps) };
        foreach (WatchStatus value in new[] { WatchStatus.Planned, WatchStatus.OnHold, WatchStatus.Dropped })
        {
            var item = new RadioMenuFlyoutItem
            {
                Text = Text(value == WatchStatus.Planned ? "Ux_ContextAutomatic" : "Anime_Status_" + value), GroupName = "AnimeStatus",
                IsChecked = value == WatchStatus.Planned ? target.Status is not (WatchStatus.OnHold or WatchStatus.Dropped) : value == target.Status,
                Command = ViewModel.ChangeStatusCommand, CommandParameter = new AnimeStatusChange(target, value)
            };
            AutomationProperties.SetAutomationId(item, "AnimeContextStatus" + value);
            status.Items.Add(item);
        }
        menu.Items.Add(status);
        menu.Items.Add(Action("Ux_ContextEdit", Symbol.Edit, ViewModel.EditAnimeCommand, target, "AnimeContextEdit"));
        menu.Items.Add(Action("Ux_ContextDownload", Symbol.Download, ViewModel.DownloadInfoCommand, target, "AnimeContextDownload"));
        menu.Items.Add(Action("Ux_ContextRemove", Symbol.Delete, ViewModel.RemoveAnimeCommand, target, "AnimeContextRemove"));
        foreach (MenuFlyoutItemBase item in menu.Items) item.IsEnabled = !ViewModel.IsBusy;
    }

    private static MenuFlyoutItem Action(string key, Symbol icon, System.Windows.Input.ICommand command, object parameter, string id)
    {
        var item = new MenuFlyoutItem { Text = Text(key), Icon = new SymbolIcon(icon), Command = command, CommandParameter = parameter };
        AutomationProperties.SetAutomationId(item, id);
        return item;
    }

    private void OnSortMenuOpening(object sender, object args)
    {
        var menu = (MenuFlyout)sender;
        menu.Items.Clear();
        foreach (AnimeSort sort in Enum.GetValues<AnimeSort>())
        {
            var item = new RadioMenuFlyoutItem { Text = Text("Ux_Sort_" + sort), GroupName = "AnimeSort", IsChecked = ViewModel.SortIndex == (int)sort };
            item.Click += (_, _) => ViewModel.SortIndex = (int)sort;
            AutomationProperties.SetAutomationId(item, "AnimeSort" + sort);
            menu.Items.Add(item);
        }
        menu.Items.Add(new MenuFlyoutSeparator());
        foreach (bool descending in new[] { false, true })
        {
            var item = new RadioMenuFlyoutItem { Text = Text(descending ? "Ux_Descending" : "Ux_Ascending"), GroupName = "AnimeSortDirection", IsChecked = ViewModel.SortDescending == descending };
            item.Click += (_, _) => ViewModel.SortDescending = descending;
            AutomationProperties.SetAutomationId(item, descending ? "AnimeSortDescending" : "AnimeSortAscending");
            menu.Items.Add(item);
        }
    }
    private static string Text(string key) => LemonLocalization.GetString(key);
}
