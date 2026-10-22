using System.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LemonVN.Views.Anime;

// View-only page projection. Native scrolling stays disabled; card objects and
// their commands remain the original ViewModel instances.
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
    private object? _sourceReference;
    private object[] _allItems = [];
    private object[]? _projection;
    private int _pageIndex, _renderedStart = -1, _renderedCount = -1;
    private long _itemsToken;
    private bool _loaded, _rendering;

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
        {
            if (_rendering) return;
            CaptureSource();
            DispatcherQueue.TryEnqueue(RenderPage);
        });
        CaptureSource();
        RenderPage();
    }
    private void Unwire()
    {
        if (_wiredTarget is null) return;
        _wiredTarget.Loaded -= OnTargetLoaded;
        _wiredTarget.SizeChanged -= OnTargetSizeChanged;
        _wiredTarget.UnregisterPropertyChangedCallback(ItemsControl.ItemsSourceProperty, _itemsToken);
        if (ReferenceEquals(_wiredTarget.ItemsSource, _projection)) _wiredTarget.ItemsSource = _sourceReference;
        _wiredTarget = null; _sourceReference = null; _projection = null; _allItems = [];
        _pageIndex = 0; _renderedStart = _renderedCount = -1;
    }
    private void CaptureSource()
    {
        if (_wiredTarget is null || ReferenceEquals(_wiredTarget.ItemsSource, _projection)) return;
        _sourceReference = _wiredTarget.ItemsSource;
        _allItems = _sourceReference is IEnumerable items ? items.Cast<object>().ToArray() : [];
        _projection = null; _pageIndex = 0; _renderedStart = _renderedCount = -1;
    }
    private void OnTargetLoaded(object sender, RoutedEventArgs args) { CaptureSource(); RenderPage(); }
    private void OnTargetSizeChanged(object sender, SizeChangedEventArgs args) => RenderPage();
    private void RenderPage()
    {
        if (!_loaded || _wiredTarget is null || _rendering) return;
        HorizontalPage page = HorizontalPaging.GetPage(_allItems.Length, _wiredTarget.ActualWidth, ItemStride, _pageIndex);
        _pageIndex = page.Index;
        PreviousButton.IsEnabled = page.Index > 0;
        NextButton.IsEnabled = page.Index + 1 < page.Pages;
        if (page.Start == _renderedStart && page.Count == _renderedCount && ReferenceEquals(_wiredTarget.ItemsSource, _projection)) return;
        _rendering = true;
        try
        {
            _projection = _allItems.Skip(page.Start).Take(page.Count).ToArray();
            _renderedStart = page.Start; _renderedCount = page.Count;
            _wiredTarget.ItemsSource = _projection;
        }
        finally { _rendering = false; }
    }
    private void OnPrevious(object sender, RoutedEventArgs args) { _pageIndex--; RenderPage(); }
    private void OnNext(object sender, RoutedEventArgs args) { _pageIndex++; RenderPage(); }
}
