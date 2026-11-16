using System.Runtime.InteropServices;
using System.Windows.Interop;
namespace ChatGPTWatchdog;
internal sealed class WpfWarningAttention(System.Windows.Window window) : IWarningAttention
{
 public bool PlaySound(string? path)=>!WindowsTarget.Locked()&&WarningSound.Play(path,p=>PlayWave(p,0,0x20003),()=>System.Media.SystemSounds.Asterisk.Play());
 public bool TryForeground()=>!WindowsTarget.Locked()&&SetForegroundWindow(new WindowInteropHelper(window).Handle);

 public void FlashTaskbar(){if(WindowsTarget.Locked())return;var data=new FlashInfo{Size=(uint)Marshal.SizeOf<FlashInfo>(),Window=new WindowInteropHelper(window).Handle,Flags=2,Count=3};FlashWindowEx(ref data);}
 [DllImport("user32.dll")] static extern bool SetForegroundWindow(nint window);
 [StructLayout(LayoutKind.Sequential)] struct FlashInfo{public uint Size;public nint Window;public uint Flags,Count,Timeout;}
 [DllImport("user32.dll")] static extern bool FlashWindowEx(ref FlashInfo data);
 [DllImport("winmm.dll",CharSet=CharSet.Unicode,EntryPoint="PlaySoundW")] static extern bool PlayWave(string path,nint module,uint flags);
}

