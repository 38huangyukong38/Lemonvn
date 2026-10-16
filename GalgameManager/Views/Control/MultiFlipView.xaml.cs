using System.Collections;
using System.Windows.Input;
using Windows.Foundation;
using CommunityToolkit.WinUI.Collections;
using DependencyPropertyGenerator;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views.Control;

[DependencyProperty<string>("Title")]
[DependencyProperty<ICommand>("TitleClickCommand")]
[DependencyProperty<object>("ItemSource")]
[DependencyProperty<DataTemplate>("ItemTemplate")]
[DependencyProperty<double>("Spacing", DefaultValue = 8.0f, DefaultBindingMode = DefaultBindingMode.OneWay)]
[DependencyProperty<ScrollMode>("ScrollMode", DefaultValue = ScrollMode.Disabled, DefaultBindingMode = DefaultBindingMode.OneWay)]
public partial class MultiFlipView
{
    public MultiFlipView()
    {
        InitializeComponent();
    }

    private void scroller_ViewChanging(object sender, ScrollViewerViewChangingEventArgs e)
    {
        if (e.FinalView.HorizontalOffset < 1) ScrollBackBtn.IsEnabled = false;
        else if (e.FinalView.HorizontalOffset > 1) ScrollBackBtn.IsEnabled = true;

        if (e.FinalView.HorizontalOffset > Scroller.ScrollableWidth - 1) ScrollForwardBtn.IsEnabled = false;
        else if (e.FinalView.HorizontalOffset < Scroller.ScrollableWidth - 1) ScrollForwardBtn.IsEnabled = true;
    }

    private void ScrollBackBtn_Click(object sender, RoutedEventArgs e)
    {
        var currentOffset = Scroller.HorizontalOffset;
        var targetPos = currentOffset - Scroller.ViewportWidth / 2;
        for (var i = Count() - 1; i >= 0; i--)
        {
            if (ItemsRepeater.TryGetElement(i) is null) continue;
            UIElement? element = ItemsRepeater.TryGetElement(i);
            if (element is null) break;
            var childStart = element.TransformToVisual(Scroller).TransformPoint(new Point(0, 0)).X;
            if (childStart > 0 && childStart < Scroller.ViewportWidth)
                targetPos = double.Min(targetPos, currentOffset - Scroller.ViewportWidth + childStart);
        }
        Scroller.ChangeView(targetPos, null, null);
    }

    private void ScrollForwardBtn_Click(object sender, RoutedEventArgs e)
    {
        var currentOffset = Scroller.HorizontalOffset;
        var targetPos = currentOffset + Scroller.ViewportWidth / 2;
        for (var i = 0; i < Count(); i++)
        {
            if (ItemsRepeater.TryGetElement(i) is null) continue;
            UIElement? element = ItemsRepeater.TryGetElement(i);
            if (element is null) break;
            var childStart = element.TransformToVisual(Scroller).TransformPoint(new Point(0, 0)).X;
            if (childStart > 0 && childStart < Scroller.ViewportWidth)
                targetPos = double.Max(targetPos, childStart + currentOffset);
        }
        Scroller.ChangeView(targetPos, null, null);
    }

    private void scroller_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateScrollButtonsVisibility();
    }

    private void UpdateScrollButtonsVisibility()
    {
        if (Scroller.ScrollableWidth > 0)
        {
            ScrollBackBtn.Visibility = Visibility.Visible;
            ScrollForwardBtn.Visibility = Visibility.Visible;
        }
        else
        {
            ScrollBackBtn.Visibility = Visibility.Collapsed;
            ScrollForwardBtn.Visibility = Visibility.Collapsed;
        }
    }

    private int Count()
    {
        IList? tmp = ItemSource as IList;
        if (ItemSource is AdvancedCollectionView)
            tmp = (ItemSource as AdvancedCollectionView)?.Source;
        return tmp?.Count ?? 0;
    }
}