using System.Collections.Generic;
using System.Linq;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views.Dialog;

public sealed partial class BatchChangePlayStatusDialog : ContentDialog
{
    public bool Canceled { get; private set; }
    public bool UploadToBgm { get; private set; }
    public bool UploadToVndb { get; private set; }
    public bool PrivateComment { get; private set; }
    public PlayType SelectedPlayType { get; private set; } = PlayType.None;
    public int SelectedRate { get; private set; }
    public IList<Galgame> SelectedGalgames { get; } = new List<Galgame>();

    private readonly int[] _rateList = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
    private readonly List<string> _playStatusList = new();
    private readonly PlayType _defaultPlayType;

    public BatchChangePlayStatusDialog(IEnumerable<Galgame> galgames, string title, PlayType defaultPlayType)
    {
        InitializeComponent();
        RequestedTheme = App.MainWindow?.Content is FrameworkElement element ? element.RequestedTheme : RequestedTheme;
        XamlRoot = App.MainWindow!.Content.XamlRoot;
        PrimaryButtonText = "Yes".GetLocalized();
        SecondaryButtonText = "Cancel".GetLocalized();
        Title = title;
        _defaultPlayType = defaultPlayType;
        if (App.MainWindow is not null)
            LayoutRoot.Width = App.MainWindow.Bounds.Width * 0.4;
        foreach (var game in galgames)
            SelectedGalgames.Add(game);
        DataContext = this;

        PrimaryButtonClick += (_, _) =>
        {
            UploadToBgm = BgmCheckBox.IsChecked ?? false;
            UploadToVndb = VndbCheckBox.IsChecked ?? false;
            PrivateComment = PrivateCheckBox.IsChecked ?? false;
            SelectedPlayType = PlayStatusBox.SelectedItem?.ToString()?.CastToPlayTyped() ?? PlayType.None;
            SelectedRate = RateBox.SelectedItem is int rate ? rate : 0;
        };
        SecondaryButtonClick += (_, _) => Canceled = true;
        Loaded += Init;

        _playStatusList.Add(PlayType.WantToPlay.GetLocalized());
        _playStatusList.Add(PlayType.Played.GetLocalized());
        _playStatusList.Add(PlayType.Playing.GetLocalized());
        _playStatusList.Add(PlayType.Shelved.GetLocalized());
        _playStatusList.Add(PlayType.Abandoned.GetLocalized());
        PlayStatusBox.ItemsSource = _playStatusList;
        RateBox.ItemsSource = _rateList;
    }

    private void Init(object sender, RoutedEventArgs routedEventArgs)
    {
        Galgame? firstGame = SelectedGalgames.FirstOrDefault();
        RateBox.SelectedItem = firstGame?.MyRate ?? 0;
        PlayType tmp = _defaultPlayType != PlayType.None ? _defaultPlayType : firstGame?.PlayType ?? PlayType.None;
        tmp = tmp == PlayType.None ? PlayType.WantToPlay : tmp;
        PlayStatusBox.SelectedItem = _playStatusList.First(x => x == tmp.GetLocalized());
        PrivateCheckBox.IsChecked = firstGame?.PrivateComment ?? false;
    }
}
