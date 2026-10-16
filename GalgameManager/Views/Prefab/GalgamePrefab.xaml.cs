using DependencyPropertyGenerator;
using GalgameManager.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace GalgameManager.Views.Prefab;

[DependencyProperty<Stretch>("ImageStretch", DefaultValue = Stretch.UniformToFill,
    DefaultBindingMode = DefaultBindingMode.OneWay)]
[DependencyProperty<Galgame>("Galgame")]
[DependencyProperty<Visibility>("PlayTypeVisibility", DefaultValue = Visibility.Collapsed,
    DefaultBindingMode = DefaultBindingMode.OneWay)]
[DependencyProperty<Visibility>("SourceVisibility", DefaultValue = Visibility.Collapsed,
    DefaultBindingMode = DefaultBindingMode.OneWay)]
[DependencyProperty<FlyoutBase>("Flyout")]
[DependencyProperty<double>("ItemScale", DefaultValue = 1.0f)]
[DependencyProperty<double>("TextHeight", DefaultValue = 80f)]
[DependencyProperty<Visibility>("NameVisibility", DefaultValue = Visibility.Visible)]
public sealed partial class GalgamePrefab
{
    public double MediumFontSize = 10f;
    private Visibility _nameVisibility;

    public GalgamePrefab()
    {
        if (Application.Current.Resources["MediumFontSize"] is double mediumFontSize)
            MediumFontSize = mediumFontSize;
        Loaded += (_, _) =>
        {
            _nameVisibility = UiDefaultValues.GamePrefabDisplayName;
            if (ReadLocalValue(NameVisibilityProperty) != DependencyProperty.UnsetValue)
                _nameVisibility = NameVisibility;
            NameTextBlock.Visibility = _nameVisibility;
            MinHeight = CalcPrefabHeight(300);
        };
        InitializeComponent();
    }

    partial void OnItemScaleChanged(double newValue)
    {
        if (newValue > 0) return;
        ItemScale = 1.0f;
    }
    
    public double CalcValue(double value) => value * ItemScale;

    public double CalcPrefabHeight(double originalHeight)
    {
        var height = originalHeight;
        if (_nameVisibility == Visibility.Collapsed)
            height -= TextHeight - 20;
        return Math.Max(height, 0) * ItemScale;
    }
}