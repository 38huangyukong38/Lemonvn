using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Xaml.Interactivity;

namespace GalgameManager.Behaviors;

public sealed class UniformGridAutoColumnSpacingBehavior : Behavior<ItemsRepeater>
{
    public double ItemWidth
    {
        get => (double)GetValue(ItemWidthProperty);
        set => SetValue(ItemWidthProperty, value);
    }

    public static readonly DependencyProperty ItemWidthProperty =
        DependencyProperty.Register(nameof(ItemWidth), typeof(double),
            typeof(UniformGridAutoColumnSpacingBehavior),
            new PropertyMetadata(0d, OnParamsChanged));

    public double MinSpacing
    {
        get => (double)GetValue(MinSpacingProperty);
        set => SetValue(MinSpacingProperty, value);
    }

    public static readonly DependencyProperty MinSpacingProperty =
        DependencyProperty.Register(nameof(MinSpacing), typeof(double),
            typeof(UniformGridAutoColumnSpacingBehavior),
            new PropertyMetadata(10d, OnParamsChanged));

    public double MaxSpacing
    {
        get => (double)GetValue(MaxSpacingProperty);
        set => SetValue(MaxSpacingProperty, value);
    }

    public static readonly DependencyProperty MaxSpacingProperty =
        DependencyProperty.Register(nameof(MaxSpacing), typeof(double),
            typeof(UniformGridAutoColumnSpacingBehavior),
            new PropertyMetadata(double.PositiveInfinity, OnParamsChanged));

    public double HorizontalPadding
    {
        get => (double)GetValue(HorizontalPaddingProperty);
        set => SetValue(HorizontalPaddingProperty, value);
    }

    public static readonly DependencyProperty HorizontalPaddingProperty =
        DependencyProperty.Register(nameof(HorizontalPadding), typeof(double),
            typeof(UniformGridAutoColumnSpacingBehavior),
            new PropertyMetadata(0d, OnParamsChanged));

    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.Loaded += OnLoaded;
        AssociatedObject.SizeChanged += OnSizeChanged;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.Loaded -= OnLoaded;
        AssociatedObject.SizeChanged -= OnSizeChanged;
        base.OnDetaching();
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => UpdateSpacing();
    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => UpdateSpacing();

    private static void OnParamsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var b = (UniformGridAutoColumnSpacingBehavior)d;
        b.UpdateSpacing();
    }

    private void UpdateSpacing()
    {
        if (AssociatedObject is null) return;
        if (AssociatedObject.Layout is not UniformGridLayout layout) return;

        var width = AssociatedObject.ActualWidth - HorizontalPadding * 2;
        if (width <= 0) return;

        var itemWidth = ItemWidth > 0 ? ItemWidth : layout.MinItemWidth;
        if (itemWidth <= 0) return;

        var minSpacing = MinSpacing;

        var columns = (int)Math.Floor((width + minSpacing) / (itemWidth + minSpacing));
        columns = Math.Max(1, columns);

        var spacing = (width - columns * itemWidth) / columns;
        if (spacing < minSpacing) spacing = minSpacing;
        if (spacing > MaxSpacing) spacing = MaxSpacing;

        layout.MinColumnSpacing = spacing;
    }
}
