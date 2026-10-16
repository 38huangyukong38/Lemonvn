using System.Collections.Specialized;
using CommunityToolkit.WinUI.Collections;
using DependencyPropertyGenerator;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Behaviors;

[DependencyProperty<GridView>("GridView")]
[DependencyProperty<double>("TargetWidth")]
[DependencyProperty<object>("ItemSource")]
[DependencyProperty<bool>("Calc")]
public partial class GridViewSpacingBehavior : UserControl
{
    partial void OnGridViewChanged(GridView? oldValue, GridView? newValue)
    {
        if (oldValue is not null)
            oldValue.SizeChanged -= OnGridViewSizeChanged;
        if (newValue is not null)
            newValue.SizeChanged += OnGridViewSizeChanged;
    }

    partial void OnItemSourceChanged(object? oldValue, object? newValue)
    {
        if (GetCollection(oldValue) is {} collection)
            collection.CollectionChanged -= CollectionOnCollectionChanged;
        if (GetCollection(newValue) is {} newCollection)
            newCollection.CollectionChanged += CollectionOnCollectionChanged;
    }

    private void CollectionOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Update();

    partial void OnTargetWidthChanged() => Update();

    private void OnGridViewSizeChanged(object sender, SizeChangedEventArgs e) => Update();

    partial void OnCalcChanged(bool newValue)
    {
        if (newValue) Update();
    }

    private void Update()
    {
        if (GridView is null || !Calc) return;
        var containerWidth = GridView.ActualWidth;
        var targetWidth = TargetWidth;
        containerWidth -= 30; // 预留冗余量
        if (targetWidth <= 0 || containerWidth <= 0) return;

        var itemsPerRow = (int)(containerWidth / targetWidth);
        if (itemsPerRow <= 0) return; // 无法放下任何子项
        var totalItemWidth = itemsPerRow * targetWidth; // 整行用于放置 item 的总宽度

        // 间距 = 剩余空间 / (itemsPerRow)，(每行的最右端也有间距（因为难以判断每个item是否在最后一列，containers里的item未必是顺序的）)
        // 如果 itemsPerRow=1 则没有间距
        var spacing = itemsPerRow > 1 ? (containerWidth - totalItemWidth) / itemsPerRow : 0;

        // 由于 GridView 子项是按顺序摆放，而且不会让我们每一行显式去控制，
        // 因此只能给所有子项设置统一的 Margin，采用“只设置右边距”的思路，让整行紧凑排布，并且最后一项右边不浪费空间。
        // 有时也会选择为所有子项设置左右对称的 spacing/2，但可能需要根据实际需求调试。
        UIElementCollection? containers = GridView.ItemsPanelRoot?.Children;
        if (containers is null || containers.Count == 0) return;
        foreach (FrameworkElement itemContainer in containers.OfType<FrameworkElement>())
            itemContainer.Margin = new Thickness(0, 0, spacing, 0);
    }

    private static INotifyCollectionChanged? GetCollection(object? value)
    {
        return value switch
        {
            INotifyCollectionChanged collection => collection,
            AdvancedCollectionView adv => adv.Source as INotifyCollectionChanged,
            _ => null,
        };
    }
}