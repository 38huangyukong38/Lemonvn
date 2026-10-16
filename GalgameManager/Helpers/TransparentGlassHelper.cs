using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using WinRT;
using WinColor = Windows.UI.Color;

namespace GalgameManager.Helpers;

public static partial class TransparentGlassHelper
{
    private static Windows.UI.Composition.Compositor? _compositor;
    /// <summary>
    /// 颜色转换：hex RGB → Color，非法输入返回白色
    /// </summary>
    public static WinColor ParseHexColor(string? hex)
    {
        if (string.IsNullOrEmpty(hex) || hex.Length != 6)
            return WinColor.FromArgb(255, 255, 255, 255);
        try
        {
            return WinColor.FromArgb(255,
                Convert.ToByte(hex[..2], 16),
                Convert.ToByte(hex[2..4], 16),
                Convert.ToByte(hex[4..6], 16));
        }
        catch
        {
            return WinColor.FromArgb(255, 255, 255, 255);
        }
    }
    public static void Apply(Window window, byte alpha = 5, byte r = 255, byte g = 255, byte b = 255)
    {
        try
        {
            var hwnd = Win32Interop.GetWindowFromWindowId(window.AppWindow.Id);
            if (hwnd == IntPtr.Zero) return;
            ExtendFrameIntoClientArea(hwnd);//扩展Frame到窗口
            EnableBlurBehind(hwnd);//启用模糊效果
            SetTransparentBackdrop(window, alpha, r, g, b);//设置透明属性
            //RemoveRoundedCorners(hwnd);//移除圆角
        }
        catch { }
    }

    public static void Remove(Window window)
    {
        try
        {
            window.As<ICompositionSupportsSystemBackdrop>().SystemBackdrop = null;
        }
        catch { }
    }

    private static void ExtendFrameIntoClientArea(IntPtr hwnd)
    {
        var margins = new MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
        _ = DwmExtendFrameIntoClientArea(hwnd, ref margins);
    }

    private static void EnableBlurBehind(IntPtr hwnd)
    {
        var rgn = CreateRectRgn(-2, -2, -1, -1);
        var blurBehind = new DWM_BLURBEHIND
        {
            dwFlags = DWM_BB.Enable | DWM_BB.BlurRegion,
            fEnable = 1,
            hRgnBlur = rgn
        };
        _ = DwmEnableBlurBehindWindow(hwnd, ref blurBehind);
        DeleteObject(rgn);
    }

    private static void SetTransparentBackdrop(Window window, byte alpha, byte r, byte g, byte b)
    {
        _compositor ??= new Windows.UI.Composition.Compositor();
        var brush = _compositor.CreateColorBrush(WinColor.FromArgb(alpha, r, g, b));
        window.As<ICompositionSupportsSystemBackdrop>().SystemBackdrop = brush;
    }

    //private static void RemoveRoundedCorners(IntPtr hwnd)
    //{
    //    var preference = 1;
    //    DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.WindowCornerPreference, ref preference, Marshal.SizeOf<int>());
    //}

    #region P/Invoke

    [StructLayout(LayoutKind.Sequential)]
    private struct MARGINS
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }

    private enum DWM_BB : int { Enable = 1, BlurRegion = 2 }

    [StructLayout(LayoutKind.Sequential)]
    private struct DWM_BLURBEHIND
    {
        public DWM_BB dwFlags;
        public int fEnable;
        public IntPtr hRgnBlur;
        public int fTransitionOnMaximized;
    }

    private enum DWMWINDOWATTRIBUTE : int { WindowCornerPreference = 33 }

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS margins);

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmEnableBlurBehindWindow(IntPtr hwnd, ref DWM_BLURBEHIND blurBehind);

    //[LibraryImport("dwmapi.dll")]
    ////private static partial int DwmSetWindowAttribute(IntPtr hwnd, DWMWINDOWATTRIBUTE attr,
    ////    ref int pvAttr, int cbAttr);

    [LibraryImport("gdi32.dll")]
    private static partial IntPtr CreateRectRgn(int x1, int y1, int x2, int y2);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteObject(IntPtr hObject);

    #endregion
}
