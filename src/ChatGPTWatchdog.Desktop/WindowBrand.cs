using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ChatGPTWatchdog;

internal sealed class WindowBrand : IDisposable
{
    System.Drawing.Icon? small, large;
    readonly Window window;
    readonly HwndSource source;
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll")] static extern int GetSystemMetricsForDpi(int index, uint dpi);
    [DllImport("user32.dll", EntryPoint="GetWindowLongW")] static extern int GetWindowLong(IntPtr window, int index);
    [DllImport("user32.dll", EntryPoint="SetWindowLongW")] static extern int SetWindowLong(IntPtr window, int index, int value);

    internal WindowBrand(Window window)
    {
        this.window = window;
        source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
        source.AddHook(Hook);
        if(window.Owner != null) SetWindowLong(source.Handle, -16, GetWindowLong(source.Handle, -16) & ~0x00020000);
        Apply();
        window.Closed += (_, _) => Dispose();
    }

    void Apply()
    {
        var handle = new WindowInteropHelper(window).Handle;
        uint dpi = GetDpiForWindow(handle);
        var nextSmall = AppBrand.LoadIcon(GetSystemMetricsForDpi(49, dpi));
        var nextLarge = AppBrand.LoadIcon(GetSystemMetricsForDpi(11, dpi));
        SendMessage(handle, 0x80, IntPtr.Zero, nextSmall.Handle);
        SendMessage(handle, 0x80, new IntPtr(1), nextLarge.Handle);
        small?.Dispose(); large?.Dispose();
        small = nextSmall; large = nextLarge;
    }

    IntPtr Hook(IntPtr handle, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == 0x02E0) window.Dispatcher.BeginInvoke(Apply);
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        source.RemoveHook(Hook);
        small?.Dispose(); large?.Dispose();
        small = large = null;
    }

    internal static bool HasCorrectNativeFrames(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        uint dpi = GetDpiForWindow(handle);
        var smallHandle = SendMessage(handle, 0x7F, IntPtr.Zero, IntPtr.Zero);
        var largeHandle = SendMessage(handle, 0x7F, new IntPtr(1), IntPtr.Zero);
        if (smallHandle == IntPtr.Zero || largeHandle == IntPtr.Zero || smallHandle == largeHandle) return false;
        using var smallIcon = System.Drawing.Icon.FromHandle(smallHandle);
        using var largeIcon = System.Drawing.Icon.FromHandle(largeHandle);
        return smallIcon.Width == GetSystemMetricsForDpi(49, dpi) && largeIcon.Width == GetSystemMetricsForDpi(11, dpi);
    }
}
