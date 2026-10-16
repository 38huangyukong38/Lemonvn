using DependencyPropertyGenerator;
using Microsoft.UI.Xaml;

namespace GalgameManager.Views.Control;

[DependencyProperty<DataTemplate>("ItemTemplate")]
[DependencyProperty<object>("ItemsSource")]
[DependencyProperty<object>("SelectedItem")]
public partial class ComboBoxWithoutNull
{
    public ComboBoxWithoutNull()
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
                SelectedItem = ComboBox.SelectedItem;
        };
    }
    
    partial void OnSelectedItemChanged(object? newValue)
    {
        if (newValue != ComboBox.SelectedItem)
            ComboBox.SelectedItem = newValue;
    }
}