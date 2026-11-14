using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Interop;

namespace LiquidGlassMonitor;

internal static class SatellitePositionV44
{
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X, Y;

        public POINT(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint flags
    );

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(
        POINT point,
        uint flags
    );

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(
        IntPtr monitor,
        ref MONITORINFO info
    );

    public static (double x, double y) Dpi(Window window)
    {
        var source = PresentationSource.FromVisual(window);
        var transform =
            source?.CompositionTarget?.TransformToDevice
            ?? Matrix.Identity;

        return (
            transform.M11 > 0 ? transform.M11 : 1.0,
            transform.M22 > 0 ? transform.M22 : 1.0
        );
    }

    public static void Move(Window window, double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y))
            return;

        var handle = new WindowInteropHelper(window).Handle;

        if (handle == IntPtr.Zero)
            return;

        const uint flags =
            0x0001 | // SWP_NOSIZE
            0x0004 | // SWP_NOZORDER
            0x0010 | // SWP_NOACTIVATE
            0x0200;  // SWP_NOOWNERZORDER

        SetWindowPos(
            handle,
            IntPtr.Zero,
            (int)Math.Round(x),
            (int)Math.Round(y),
            0,
            0,
            flags
        );
    }

    public static (double x, double y) KeepOnScreen(
        double x,
        double y,
        double width,
        double height,
        double anchorX,
        double anchorY
    )
    {
        var monitor = MonitorFromPoint(
            new POINT(
                (int)Math.Round(anchorX),
                (int)Math.Round(anchorY)
            ),
            2
        );

        var info = new MONITORINFO
        {
            cbSize = Marshal.SizeOf<MONITORINFO>()
        };

        if (monitor == IntPtr.Zero ||
            !GetMonitorInfo(monitor, ref info))
        {
            return (x, y);
        }

        double left = info.rcWork.Left;
        double top = info.rcWork.Top;
        double right = info.rcWork.Right;
        double bottom = info.rcWork.Bottom;

        x = Math.Max(
            left,
            Math.Min(x, Math.Max(left, right - width))
        );

        y = Math.Max(
            top,
            Math.Min(y, Math.Max(top, bottom - height))
        );

        return (x, y);
    }
}