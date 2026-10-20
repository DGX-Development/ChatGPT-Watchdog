using System.Media;
using System.Runtime.InteropServices;

namespace ChatGPTWatchdog;

internal sealed class WindowsWarningAttention(Form window) : IWarningAttention
{
    public bool PlaySound(string? path) => !WindowsTarget.Locked() && Play(path);
    internal static bool Play(string? path) => WarningSound.Play(path,
        file => PlayWave(file, 0, 0x20003), () => SystemSounds.Asterisk.Play());
    public bool TryForeground() => !WindowsTarget.Locked() && SetForegroundWindow(window.Handle);
    public void FlashTaskbar()
    {
        if (WindowsTarget.Locked()) return;
        var info = new FlashInfo { Size = (uint)Marshal.SizeOf<FlashInfo>(), Window = window.Handle, Flags = 2, Count = 3 };
        _ = FlashWindowEx(ref info);
    }
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)] static extern bool SetForegroundWindow(nint window);
    [StructLayout(LayoutKind.Sequential)]
    struct FlashInfo { public uint Size; public nint Window; public uint Flags; public uint Count; public uint Timeout; }
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)] static extern bool FlashWindowEx(ref FlashInfo info);
    [DllImport("winmm.dll", CharSet = CharSet.Unicode, EntryPoint = "PlaySoundW")]
    [return: MarshalAs(UnmanagedType.Bool)] static extern bool PlayWave(string path, nint module, uint flags);
}
