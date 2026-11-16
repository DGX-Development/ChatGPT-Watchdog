using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace ChatGPTWatchdog;
internal sealed class NativeShutdownTarget : IShutdownTarget
{
    readonly SafeProcessHandle handle;
    readonly int pid, session;
    readonly long started;
    readonly string path;
    readonly string? family;
    readonly Func<bool>? stillAllowed;
    public string Identity => $"pid={pid}; startedUtcTicks={started}; session={session}";
    public NativeShutdownTarget(int pid, long started, string? family, string? requiredPath = null, Func<bool>? stillAllowed = null)
    {
        this.pid = pid; this.started = started; this.family = family; this.stillAllowed = stillAllowed;
        using var self = Process.GetCurrentProcess(); session = self.SessionId;
        handle = OpenProcess(0x1000 | 0x100000, false, pid);
        try
        {
            if (handle.IsInvalid) throw Error("OpenProcess(query/synchronize)");
            path = PathOf(handle);
            if (requiredPath is not null && !string.Equals(path, requiredPath, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Unexpected executable path.");
            Verify(handle);
        }
        catch { handle.Dispose(); throw; }
    }
    static Win32Exception Error(string operation)
    {
        int code = Marshal.GetLastWin32Error();
        return new Win32Exception(code, $"{operation} failed (Windows error {code}: {new Win32Exception(code).Message}).");
    }
    static string PathOf(SafeProcessHandle process)
    {
        uint length = 32768; var text = new StringBuilder((int)length);
        if (!QueryFullProcessImageName(process, 0, text, ref length)) throw Error("QueryFullProcessImageName");
        return text.ToString();
    }
    void Verify(SafeProcessHandle process)
    {
        if (!GetProcessTimes(process, out long created, out _, out _, out _)) throw Error("GetProcessTimes");
        if (DateTime.FromFileTimeUtc(created).Ticks != started || !ProcessIdToSessionId((uint)pid, out uint actualSession) || actualSession != session ||
            !string.Equals(PathOf(process), path, StringComparison.OrdinalIgnoreCase)) throw new IOException("Process identity changed.");
        if (family is not null)
        {
            uint length = 0; if (GetPackageFamilyName(process, ref length, null) != 122) throw new IOException("Package identity unavailable.");
            var value = new StringBuilder((int)length);
            if (GetPackageFamilyName(process, ref length, value) != 0 || value.ToString() != family) throw new IOException("Unexpected package identity.");
        }
    }
    public ProcessObservation Observe()
    {
        uint wait = WaitForSingleObject(handle, 0);
        return wait == 0 ? ProcessObservation.Exited : wait == 258 ? ProcessObservation.Running : ProcessObservation.Unknown;
    }
    public ShutdownResult Force()
    {
        try
        {
            if (Observe() == ProcessObservation.Exited) return new(true, "Original process already exited.");
            // Obtain termination rights only at escalation, then validate and act using this same handle.
            using var terminable = OpenProcess(0x1000 | 0x100000 | 1, false, pid);
            if (terminable.IsInvalid) throw Error("OpenProcess(terminate)");
            Verify(terminable);
            if (WaitForSingleObject(terminable, 0) == 0) return new(true, "Original process already exited.");
            if (stillAllowed is not null && !stillAllowed()) return new(false, "Force close cancelled because activity or monitoring state changed.");
            if (!TerminateProcess(terminable, 1)) throw Error("TerminateProcess");
            return new(true, "Termination requested for the verified single process. No name-based or process-tree termination.");
        }
        catch (Exception e) when (e is Win32Exception or IOException) { return new(false, e.Message); }
    }
    public void Dispose() => handle.Dispose();
    [DllImport("kernel32.dll", SetLastError=true)] static extern SafeProcessHandle OpenProcess(uint access,bool inherit,int pid);
    [DllImport("kernel32.dll", SetLastError=true)] static extern uint WaitForSingleObject(SafeProcessHandle process,uint milliseconds);
    [DllImport("kernel32.dll", SetLastError=true)] static extern bool GetProcessTimes(SafeProcessHandle process,out long created,out long exited,out long kernel,out long user);
    [DllImport("kernel32.dll", SetLastError=true)] static extern bool ProcessIdToSessionId(uint pid,out uint session);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern bool QueryFullProcessImageName(SafeProcessHandle process,uint flags,StringBuilder path,ref uint length);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] static extern int GetPackageFamilyName(SafeProcessHandle process,ref uint length,StringBuilder? name);
    [DllImport("kernel32.dll", SetLastError=true)] static extern bool TerminateProcess(SafeProcessHandle process,uint code);
}
