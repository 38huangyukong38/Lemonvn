using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Xaml.Interactivity;

namespace GalgameManager.Behaviors;

/// <summary>
/// 根据控件宽度调整控件的左右Margin
/// </summary>
public class ResponsivePaddingBehavior : Behavior<StackPanel>
{
    public double OrinWidth
    {
        get => (double)GetValue(OrinWidthProperty);
        set => SetValue(OrinWidthProperty, value);
    }

    public static readonly DependencyProperty OrinWidthProperty =
        DependencyProperty.Register(nameof(OrinWidth), typeof(double),
            typeof(ResponsivePaddingBehavior), new PropertyMetadata(1000));
    
    public Thickness OrinPadding
    {
        get => (Thickness)GetValue(OrinPaddingProperty);
        set => SetValue(OrinPaddingProperty, value);
    }
    
    public static readonly DependencyProperty OrinPaddingProperty =
        DependencyProperty.Register(nameof(OrinPadding), typeof(Thickness),
            typeof(ResponsivePaddingBehavior), new PropertyMetadata(new Thickness(0)));

    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.SizeChanged += OnSizeChanged;
        UpdatePadding();
    }

    protected override void OnDetaching()
    {
        AssociatedObject.SizeChanged -= OnSizeChanged;
        base.OnDetaching();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => UpdatePadding();

    private void UpdatePadding()
    {
        var left = AssociatedObject.ActualWidth / OrinWidth * OrinPadding.Left;
        var right = AssociatedObject.ActualWidth / OrinWidth * OrinPadding.Right;
        AssociatedObject.Padding = new Thickness(left, OrinPadding.Top, right, OrinPadding.Bottom);
    }
}