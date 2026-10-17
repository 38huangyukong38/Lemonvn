using LemonVN.ViewModels.Anime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LemonVN.Views.Anime;

public sealed partial class AnimeCardControl : UserControl
{
    public static readonly DependencyProperty CardProperty = DependencyProperty.Register(nameof(Card),
        typeof(AnimeCardViewModel), typeof(AnimeCardControl), new PropertyMetadata(null));
    public AnimeCardViewModel Card
    {
        get => (AnimeCardViewModel)GetValue(CardProperty);
        set => SetValue(CardProperty, value);
    }
    public static readonly DependencyProperty IsCompactProperty = DependencyProperty.Register(nameof(IsCompact),
        typeof(bool), typeof(AnimeCardControl), new PropertyMetadata(false, OnLayoutModeChanged));
    public bool IsCompact
    {
        get => (bool)GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }
    private XamlRoot? _observedRoot;

    public AnimeCardControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        _observedRoot = XamlRoot;
        if (_observedRoot is not null) _observedRoot.Changed += OnRootChanged;
        UpdateLayoutMode();
        UpdateBottomPadding();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        if (_observedRoot is not null) _observedRoot.Changed -= OnRootChanged;
        _observedRoot = null;
    }

    private void OnRootChanged(XamlRoot sender, XamlRootChangedEventArgs args) => UpdateBottomPadding();

    private static void OnLayoutModeChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((AnimeCardControl)sender).UpdateLayoutMode();

    private void UpdateLayoutMode()
    {
        if (CardLayout is null) return;
        Width = IsCompact ? 144 : 190;
        CoverRow.Height = new GridLength(IsCompact ? 180 : 244);
        TitleRow.MinHeight = IsCompact ? 36 : 42;
        CardTitle.LineHeight = IsCompact ? 16 : 18;
        CardTitle.Margin = new Thickness(0, IsCompact ? 4 : 6, 0, 0);
        if (IsCompact) CardTitle.FontSize = 12;
        else CardTitle.ClearValue(TextBlock.FontSizeProperty);
        PersonalInfo.Visibility = IsCompact ? Visibility.Collapsed : Visibility.Visible;
    }

    private string ProgressLabel(AnimeCardViewModel? card, bool compact) => card is null ? string.Empty
        : compact ? card.Status + " · " + card.ProgressText : card.ProgressText;

    private void UpdateBottomPadding() => CardLayout.Padding = new Thickness(8, 8, 8,
        AnimeCardLayoutMetrics.BottomPadding(_observedRoot?.RasterizationScale ?? 1));

    // ItemsWrapGrid measures its uniform height from the first card. When its total
    // is unknown, move the bar's 3 + 3 DIP above the text instead of losing that
    // height or leaving a blank footer. Both card kinds keep the same extent.
    private Thickness ProgressTextMargin(bool hasKnownTotal) => new(0, hasKnownTotal ? 4 : 10, 0, 0);

}
