using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using CommunityToolkit.WinUI;

namespace GalgameManager.Views.Control;

public sealed partial class HotkeyInputBox : INotifyPropertyChanged
{
    private readonly HashSet<VirtualKey> _pressedKeys = new();
    private bool _isCapturing;
    private string _buttonText = "";
    private string _hotkeyDisplayText = "";
    private Visibility _showPlaceholder = Visibility.Visible;
    private Visibility _hideText = Visibility.Collapsed;

    public HotkeyInputBox()
    {
        InitializeComponent();
        UpdateButtonText();
    }

    public static readonly DependencyProperty PlaceholderTextProperty =
        DependencyProperty.Register(nameof(PlaceholderText), typeof(string), typeof(HotkeyInputBox), 
            new PropertyMetadata("HotkeyInputBox_PlaceholderText".GetLocalized()));

    public string PlaceholderText
    {
        get => (string)GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    public static readonly DependencyProperty HotkeyKeysProperty =
        DependencyProperty.Register(nameof(HotkeyKeys), typeof(List<int>), typeof(HotkeyInputBox),
            new PropertyMetadata(new List<int>(), OnHotkeyKeysChanged));

    public List<int> HotkeyKeys
    {
        get => (List<int>)GetValue(HotkeyKeysProperty);
        set => SetValue(HotkeyKeysProperty, value);
    }

    private static void OnHotkeyKeysChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is HotkeyInputBox control)
        {
            control.UpdateDisplay();
        }
    }

    public string ButtonText
    {
        get => _buttonText;
        private set
        {
            _buttonText = value;
            OnPropertyChanged();
        }
    }

    public string HotkeyDisplayText
    {
        get => _hotkeyDisplayText;
        private set
        {
            _hotkeyDisplayText = value;
            OnPropertyChanged();
        }
    }

    public Visibility ShowPlaceholder
    {
        get => _showPlaceholder;
        private set
        {
            _showPlaceholder = value;
            OnPropertyChanged();
        }
    }

    public Visibility HideText
    {
        get => _hideText;
        private set
        {
            _hideText = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void InputButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isCapturing)
        {
            StartCapturing();
        }
    }

    private void InputButton_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!_isCapturing) return;

        e.Handled = true;
        
        // 检查是否是修饰键
        if (IsModifierKey(e.Key))
        {
            _pressedKeys.Add(e.Key);
        }
        else if (_pressedKeys.Count > 0) // 必须有修饰键才能完成快捷键设置
        {
            _pressedKeys.Add(e.Key);
            CompleteCapture();
        }
        else
        {
            // 如果没有修饰键，只记录当前按键但不完成设置
            _pressedKeys.Clear();
            _pressedKeys.Add(e.Key);
        }

        UpdateCaptureDisplay();
    }

    private void InputButton_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_isCapturing)
        {
            CancelCapture();
        }
    }

    private void StartCapturing()
    {
        _isCapturing = true;
        _pressedKeys.Clear();
        ButtonText = "HotkeyInputBox_StartCapturing".GetLocalized() ?? "请按下快捷键";
        ShowPlaceholder = Visibility.Collapsed;
        HideText = Visibility.Collapsed;
        InputButton.Focus(FocusState.Programmatic);
    }

    private void CompleteCapture()
    {
        _isCapturing = false;
        
        List<VirtualKey> keys = _pressedKeys.OrderBy(GetKeyOrder).ToList();
        HotkeyKeys = keys.Select(k => (int)k).ToList();
        
        UpdateDisplay();
        OnHotkeyChanged();
    }

    private void CancelCapture()
    {
        _isCapturing = false;
        UpdateDisplay();
    }

    private void UpdateCaptureDisplay()
    {
        if (_isCapturing && _pressedKeys.Count > 0)
        {
            IOrderedEnumerable<VirtualKey> sortedKeys = _pressedKeys.OrderBy(GetKeyOrder);
            ButtonText = string.Join(" + ", sortedKeys.Select(GetKeyDisplayName));
        }
    }

    private void UpdateDisplay()
    {
        if (HotkeyKeys.Count == 0)
        {
            ButtonText = "";
            HotkeyDisplayText = "";
            ShowPlaceholder = Visibility.Visible;
            HideText = Visibility.Collapsed;
        }
        else
        {
            IOrderedEnumerable<VirtualKey> keys = HotkeyKeys.Select(k => (VirtualKey)k).OrderBy(GetKeyOrder);
            var displayText = string.Join(" + ", keys.Select(GetKeyDisplayName));
            
            if (_isCapturing)
            {
                ButtonText = displayText;
                HotkeyDisplayText = "";
                ShowPlaceholder = Visibility.Collapsed;
                HideText = Visibility.Collapsed;
            }
            else
            {
                ButtonText = "";
                HotkeyDisplayText = displayText;
                ShowPlaceholder = Visibility.Collapsed;
                HideText = Visibility.Visible;
            }
        }
        
        UpdateButtonText();
    }

    private void UpdateButtonText()
    {
        if (!_isCapturing && (HotkeyKeys.Count == 0))
        {
            ButtonText = "HotkeyInputBox_ClickToSet".GetLocalized() ?? "点击设置快捷键";
        }
    }

    private static bool IsModifierKey(VirtualKey key)
    {
        return key is VirtualKey.Control or VirtualKey.LeftControl or VirtualKey.RightControl
                    or VirtualKey.Shift or VirtualKey.LeftShift or VirtualKey.RightShift
                    or VirtualKey.Menu or VirtualKey.LeftMenu or VirtualKey.RightMenu
                    or VirtualKey.LeftWindows or VirtualKey.RightWindows;
    }

    private static int GetKeyOrder(VirtualKey key)
    {
        return key switch
        {
            VirtualKey.LeftWindows or VirtualKey.RightWindows => 1,
            VirtualKey.Control or VirtualKey.LeftControl or VirtualKey.RightControl => 2,
            VirtualKey.Menu or VirtualKey.LeftMenu or VirtualKey.RightMenu => 3,
            VirtualKey.Shift or VirtualKey.LeftShift or VirtualKey.RightShift => 4,
            _ => 5
        };
    }

    private static string GetKeyDisplayName(VirtualKey key)
    {
        return key switch
        {
            VirtualKey.LeftWindows or VirtualKey.RightWindows => "Win",
            VirtualKey.Control or VirtualKey.LeftControl or VirtualKey.RightControl => "Ctrl",
            VirtualKey.Menu or VirtualKey.LeftMenu or VirtualKey.RightMenu => "Alt",
            VirtualKey.Shift or VirtualKey.LeftShift or VirtualKey.RightShift => "Shift",
            VirtualKey.Space => "Space",
            VirtualKey.Tab => "Tab",
            VirtualKey.Enter => "Enter",
            VirtualKey.Escape => "Esc",
            VirtualKey.Back => "Backspace",
            VirtualKey.Delete => "Delete",
            VirtualKey.Insert => "Insert",
            VirtualKey.Home => "Home",
            VirtualKey.End => "End",
            VirtualKey.PageUp => "Page Up",
            VirtualKey.PageDown => "Page Down",
            VirtualKey.Up => "↑",
            VirtualKey.Down => "↓",
            VirtualKey.Left => "←",
            VirtualKey.Right => "→",
            VirtualKey.F1 => "F1",
            VirtualKey.F2 => "F2",
            VirtualKey.F3 => "F3",
            VirtualKey.F4 => "F4",
            VirtualKey.F5 => "F5",
            VirtualKey.F6 => "F6",
            VirtualKey.F7 => "F7",
            VirtualKey.F8 => "F8",
            VirtualKey.F9 => "F9",
            VirtualKey.F10 => "F10",
            VirtualKey.F11 => "F11",
            VirtualKey.F12 => "F12",
            _ => key.ToString()
        };
    }

    public event EventHandler? HotkeyChanged;
    
    private void OnHotkeyChanged()
    {
        HotkeyChanged?.Invoke(this, EventArgs.Empty);
    }
} 