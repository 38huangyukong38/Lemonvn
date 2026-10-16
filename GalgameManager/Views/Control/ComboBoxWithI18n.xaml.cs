using DependencyPropertyGenerator;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views.Control;

[DependencyProperty<DataTemplate>("ItemTemplate")]
[DependencyProperty<object>("ItemsSource")]
[DependencyProperty<object>("SelectedItem")]
public partial class ComboBoxWithI18N
{
    public event Action<object?>? SelectedItemChangedEvent;
    
    public ComboBoxWithI18N()
    {
        InitializeComponent();
        ComboBox.SelectionChanged += (_, _) =>
        {
            // 本质是强制刷新 SelectedItem的值
            if (ComboBox.SelectedItem == null)
            {
                var tmp = SelectedItem;
                ComboBox.SelectedIndex = 0;
                SelectedItem = ComboBox.SelectedItem;
                SelectedItem = tmp;
            }

            if (ComboBox.SelectedItem is not null && ComboBox.SelectedItem != SelectedItem)
            {
                SelectedItem = ComboBox.SelectedItem;
                SelectedItemChangedEvent?.Invoke(SelectedItem);
            }
        };
    }

    partial void OnSelectedItemChanged(object? newValue)
    {
        if (newValue != ComboBox.SelectedItem && newValue != null)
            ComboBox.SelectedItem = newValue;
    }
}

public class ComboBoxWithI18NDataTemplateSelector : DataTemplateSelector
{
    public DataTemplate DefaultTemplate { get; set; } = null!;
    public DataTemplate? ItemTemplate { get; set; }

    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
    {
        return ItemTemplate ?? DefaultTemplate;
    }
}