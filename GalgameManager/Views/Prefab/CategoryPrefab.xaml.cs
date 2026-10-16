using DependencyPropertyGenerator;
using GalgameManager.Models;

namespace GalgameManager.Views.Prefab;

[DependencyProperty<Category>("Category")]
[DependencyProperty<double>("ItemScale", DefaultValue = 1.0f)]
[DependencyProperty<bool>("GameCntVisible", DefaultValue = true)]
public partial class CategoryPrefab
{
    public CategoryPrefab()
    {
        InitializeComponent();
    }
    
    public double CalcValue(double value) => value * ItemScale;
}