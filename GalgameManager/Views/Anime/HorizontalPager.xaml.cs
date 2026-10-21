using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace LemonVN.Views.Anime;

// View-only paging over the native virtualizing ListView's own ScrollViewer.
public sealed partial class HorizontalPager : UserControl
{
    public string PreviousLabel => LemonVN.Helpers.LemonLocalization.GetString("Browse_PreviousItems");
    public string NextLabel => LemonVN.Helpers.LemonLocalization.GetString("Browse_NextItems");
    public static readonly DependencyProperty TargetProperty = DependencyProperty.Register(nameof(Target),
        typeof(ListViewBase), typeof(HorizontalPager), new PropertyMetadata(null, OnTargetChanged));
    public ListViewBase? Target
    {
        get => (ListViewBase?)GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }
    public double ItemStride { get; set; } = 156;
    private ListViewBase? _wiredTarget;
    private ScrollViewer? _viewer;
    private long _itemsToken;
    private long _extentToken, _viewportToken;
    private bool _loaded;

    public HorizontalPager()
    {
        InitializeComponent();
        Loaded += (_, _) => { _loaded = true; Wire(); };
        Unloaded += (_, _) => { _loaded = false; Unwire(); };
    }
    private static void OnTargetChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) => ((HorizontalPager)sender).Wire();
    private void Wire()
    {
        Unwire();
        if (!_loaded || Target is null) return;
        _wiredTarget = Target;
        _wiredTarget.Loaded += OnTargetLoaded;
        _wiredTarget.SizeChanged += OnTargetSizeChanged;
        _itemsToken = _wiredTarget.RegisterPropertyChangedCallback(ItemsControl.ItemsSourceProperty, (_, _) =>
            DispatcherQueue.TryEnqueue(AttachViewer));
        AttachViewer();
    }
    private void Unwire()
    {
        DetachViewer();
        if (_wiredTarget is null) return;
        _wiredTarget.Loaded -= OnTargetLoaded;
        _wiredTarget.SizeChanged -= OnTargetSizeChanged;
        _wiredTarget.UnregisterPropertyChangedCallback(ItemsControl.ItemsSourceProperty, _itemsToken);
        _wiredTarget = null;
    }
    private void OnTargetLoaded(object sender, RoutedEventArgs args) => AttachViewer();
    private void OnTargetSizeChanged(object sender, SizeChangedEventArgs args) => AttachViewer();
    private void AttachViewer()
    {
        if (!_loaded || _wiredTarget is null) return;
        ScrollViewer? viewer = FindViewer(_wiredTarget);
        if (!ReferenceEquals(_viewer, viewer))
        {
            DetachViewer();
            _viewer = viewer;
            if (_viewer is not null)
            {
                _viewer.ViewChanged += OnViewChanged;
                _extentToken = _viewer.RegisterPropertyChangedCallback(ScrollViewer.ScrollableWidthProperty, (_, _) => RefreshButtons());
                _viewportToken = _viewer.RegisterPropertyChangedCallback(ScrollViewer.ViewportWidthProperty, (_, _) => RefreshButtons());
            }
        }
        RefreshButtons();
    }
    private void DetachViewer()
    {
        if (_viewer is null) return;
        _viewer.ViewChanged -= OnViewChanged;
        _viewer.UnregisterPropertyChangedCallback(ScrollViewer.ScrollableWidthProperty, _extentToken);
        _viewer.UnregisterPropertyChangedCallback(ScrollViewer.ViewportWidthProperty, _viewportToken);
        _viewer = null;
    }
    private static ScrollViewer? FindViewer(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            if (child is ScrollViewer viewer) return viewer;
            if (FindViewer(child) is { } found) return found;
        }
        return null;
    }
    private void OnViewChanged(object? sender, ScrollViewerViewChangedEventArgs args) => RefreshButtons();
    private void RefreshButtons()
    {
        PreviousButton.IsEnabled = _viewer is { HorizontalOffset: > 0.5 };
        NextButton.IsEnabled = _viewer is not null && _viewer.HorizontalOffset < _viewer.ScrollableWidth - 0.5;
    }
    private void OnPrevious(object sender, RoutedEventArgs args) => Move(false);
    private void OnNext(object sender, RoutedEventArgs args) => Move(true);
    private void Move(bool forward)
    {
        if (_viewer is null) return;
        _viewer.ChangeView(HorizontalPaging.NextOffset(_viewer.HorizontalOffset, _viewer.ViewportWidth,
            _viewer.ScrollableWidth, ItemStride, forward), null, null);
    }
}
