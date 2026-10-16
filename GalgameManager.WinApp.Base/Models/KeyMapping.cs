using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GalgameManager.Models;

public partial class KeyMapping : ObservableObject
{
    [ObservableProperty]
    private List<int> _from = new();

    [ObservableProperty]
    private List<int> _to = new();

    [ObservableProperty]
    private string _remark = string.Empty;

    [ObservableProperty]
    private bool _isEnabled = true;

    [ObservableProperty]
    private bool _isGlobal;

    /// <summary>
    /// 是否包含鼠标按键映射
    /// </summary>
    public bool ContainsMouseKey => To.Any(IsMouseKey);

    private static bool IsMouseKey(int keyCode) => keyCode switch
    {
        >= 1 and <= 7 => true, // 鼠标左键(1), 右键(2), 中键(3), X1(4), X2(5), WheelUp(6), WheelDown(7)
        _ => false
    };
}