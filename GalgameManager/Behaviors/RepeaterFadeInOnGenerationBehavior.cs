using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.Xaml.Interactivity;
using DependencyPropertyGenerator;


namespace GalgameManager.Behaviors;

[DependencyProperty<int>("Tick")]
[DependencyProperty<double>("FadeDurationMilliseconds")]
public partial class RepeaterFadeInOnGenerationBehavior : Behavior<ItemsRepeater>
{
    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject is null) return;

        AssociatedObject.ElementPrepared += OnElementPrepared;
        AssociatedObject.ElementClearing += OnElementClearing;
    }

    protected override void OnDetaching()
    {
        if (AssociatedObject is not null)
        {
            AssociatedObject.ElementPrepared -= OnElementPrepared;
            AssociatedObject.ElementClearing -= OnElementClearing;
        }

        base.OnDetaching();
    }

    private void OnElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        if (args.Element is not FrameworkElement element) return;

        // 只有当 tick 变了才播放
        if (Equals(Tick, element.Tag)) return;
        element.Tag = Tick;

        var visual = ElementCompositionPreview.GetElementVisual(element);

        // duration <= 0 直接显示
        var ms = FadeDurationMilliseconds;
        if (ms <= 0)
        {
            visual.StopAnimation("Opacity");
            visual.Opacity = 1f;
            return;
        }

        visual.StopAnimation("Opacity");
        visual.Opacity = 0f;

        var compositor = visual.Compositor;
        var anim = compositor.CreateScalarKeyFrameAnimation();
        anim.InsertKeyFrame(1f, 1f);
        anim.Duration = TimeSpan.FromMilliseconds(ms);

        visual.StartAnimation("Opacity", anim);
    }

    private void OnElementClearing(ItemsRepeater sender, ItemsRepeaterElementClearingEventArgs args)
    {
        if (args.Element is not FrameworkElement element) return;

        var visual = ElementCompositionPreview.GetElementVisual(element);
        visual.StopAnimation("Opacity");
        visual.Opacity = 1f;
    }
}
