using System.Collections.ObjectModel;
using GalgameManager.Helpers;
using GalgameManager.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views.Dialog;

public sealed partial class BatchConfirmDialog : ContentDialog
{
    public ObservableCollection<Galgame> SelectedGalgames { get; } = new();
    public string? Message { get; }
    public Visibility MessageVisibility => string.IsNullOrEmpty(Message) ? Visibility.Collapsed : Visibility.Visible;

    public BatchConfirmDialog(IEnumerable<Galgame> galgames, string title, string? message)
    {
        InitializeComponent();
        RequestedTheme = App.MainWindow?.Content is FrameworkElement element ? element.RequestedTheme : RequestedTheme;
        Title = title;
        Message = message;
        if (App.MainWindow is not null)
            LayoutRoot.Width = App.MainWindow.Bounds.Width * 0.4;
        foreach (var game in galgames)
            SelectedGalgames.Add(game);
        PrimaryButtonText = "Yes".GetLocalized();
        SecondaryButtonText = "Cancel".GetLocalized();
        DataContext = this;
    }
}
