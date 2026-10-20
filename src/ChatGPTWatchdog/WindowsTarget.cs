using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace ChatGPTWatchdog;

internal readonly record struct InputSample(uint Token, long IdleMilliseconds);
internal sealed record TargetWindow(nint Handle, int ProcessId, long Started);
internal sealed record TargetSnapshot(bool Known, string? Key, IReadOnlyList<TargetWindow> Windows,
    IReadOnlyList<int> ProcessIds, string? Error = null);

internal static class WindowsTarget
{
    const string Family = "OpenAI.Codex_2p2nqsd0c76g0";
    public static InputSample? Input()
    {
        var value = new LASTINPUTINFO { Size = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!GetLastInputInfo(ref value)) return null;
        uint age = unchecked((uint)Environment.TickCount64 - value.Time);
        return age > int.MaxValue ? null : new(value.Time, age);
    }
    public static TargetSnapshot Read()
    {
        var processes = new List<(int Id, long Started)>();
        var windows = new List<TargetWindow>();
        try
        {
            using var self = Process.GetCurrentProcess();
            int session = self.SessionId;
            foreach (var process in Process.GetProcessesByName("ChatGPT"))
            {
                using (process)
                {
                    try
                    {
                        if (process.SessionId != session) continue;
                        using var handle = OpenProcess(0x1000, false, process.Id);
                        if (handle.IsInvalid) throw new IOException("ChatGPT identity could not be checked.");
                        uint length = 0;
                        int result = GetPackageFamilyName(handle, ref length, null);
                        if (result == 15700) continue; // Unpackaged applications are not our target.
                        if (result != 122) throw new IOException("ChatGPT package could not be checked.");
                        var family = new StringBuilder((int)length);
                        if (GetPackageFamilyName(handle, ref length, family) != 0)
                            throw new IOException("ChatGPT package could not be checked.");
                        if (family.ToString() != Family) continue;
                        length = 32768;
                        var path = new StringBuilder((int)length);
                        if (!QueryFullProcessImageName(handle, 0, path, ref length))
                            throw new IOException("ChatGPT location could not be checked.");
                        uint packageLength = 0;
                        if (GetPackageFullName(handle, ref packageLength, null) != 122)
                            throw new IOException("ChatGPT package could not be checked.");
                        var fullName = new StringBuilder((int)packageLength);
                        if (GetPackageFullName(handle, ref packageLength, fullName) != 0)
                            throw new IOException("ChatGPT package could not be checked.");
                        uint rootLength = 0;
                        if (GetPackagePathByFullName(fullName.ToString(), ref rootLength, null) != 122)
                            throw new IOException("ChatGPT installation could not be checked.");
                        var root = new StringBuilder((int)rootLength);
                        if (GetPackagePathByFullName(fullName.ToString(), ref rootLength, root) != 0 ||
                            !string.Equals(path.ToString(), Path.Combine(root.ToString(), "app", "ChatGPT.exe"), StringComparison.OrdinalIgnoreCase))
                            throw new IOException("Unexpected ChatGPT executable location.");
                        processes.Add((process.Id, process.StartTime.ToUniversalTime().Ticks));
                    }
                    catch (InvalidOperationException) { /* Process exited during discovery. */ }
                    catch (ArgumentException) { /* Process exited during discovery. */ }
                }
            }
            if (processes.Count == 0) return new(true, null, [], []);
            if (!EnumWindows((window, _) =>
            {
                GetWindowThreadProcessId(window, out uint pid);
                var entry = processes.Find(p => p.Id == pid);
                if (entry != default && GetWindow(window, 4) == 0 && AppWindow(window))
                    windows.Add(new(window, entry.Id, entry.Started));
                return true;
            }, 0)) throw new IOException("ChatGPT windows could not be checked.");
            // GUI process identity is stable while renderer/utility helpers come and go.
            var principals = windows.Count > 0
                ? processes.Where(p => windows.Any(w => w.ProcessId == p.Id)) : processes;
            string key = string.Join(";", principals.OrderBy(p => p.Id).Select(p => $"{p.Id}:{p.Started}"));
            return new(true, key, windows, processes.Select(p => p.Id).ToArray());
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException)
        { return new(false, null, [], [], error.Message); }
    }
    static bool AppWindow(nint window)
    {
        var name = new StringBuilder(256); GetClassName(window, name, name.Capacity);
        return name.ToString() == "Chrome_WidgetWin_1";
    }
    public static IShutdownTarget Capture(TargetSnapshot expected, Func<bool>? stillAllowed = null)
    {
        var current = Read();
        if (!current.Known || current.Key != expected.Key) throw new IOException(current.Error ?? "Application identity changed.");
        var main = current.Windows.DistinctBy(w => w.ProcessId).ToArray();
        if (main.Length != 1) throw new IOException("Cannot uniquely identify the monitored application process.");
        return new NativeShutdownTarget(main[0].ProcessId, main[0].Started, Family, stillAllowed: stillAllowed);
    }
    public static bool Locked() => LockState() ?? true;
    // Win+L does not reliably change the desktop name on current Windows 11.
    // Query the actual lock flag for our own session. Unknown is never reported as unlocked.
    public static bool? LockState()
    {
        using var self = Process.GetCurrentProcess();
        if (!WTSQuerySessionInformation(0, (uint)self.SessionId, 25, out nint buffer, out uint bytes)) return null;
        try
        {
            if (bytes < Marshal.SizeOf<SessionInfo>()) return null;
            var info = Marshal.PtrToStructure<SessionInfo>(buffer);
            if (info.Level != 1 || info.Data.SessionId != self.SessionId) return null;
            return info.Data.Flags switch { 0 => true, 1 => false, _ => null };
        }
        finally { WTSFreeMemory(buffer); }
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct SessionData
    {
        public uint SessionId; public int State, Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)] public string Station;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 21)] public string User;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 18)] public string Domain;
        public long Logon, Connect, Disconnect, LastInput, Current;
        public uint IncomingBytes, OutgoingBytes, IncomingFrames, OutgoingFrames, IncomingCompressed, OutgoingCompressed;
    }
    [StructLayout(LayoutKind.Sequential)] struct SessionInfo { public uint Level; public SessionData Data; }
    [DllImport("wtsapi32.dll", EntryPoint = "WTSQuerySessionInformationW", SetLastError = true)]
    static extern bool WTSQuerySessionInformation(nint server, uint session, int information, out nint buffer, out uint bytes);
    [DllImport("wtsapi32.dll")] static extern void WTSFreeMemory(nint buffer);

    [StructLayout(LayoutKind.Sequential)] struct LASTINPUTINFO { public uint Size, Time; }
    delegate bool WindowCallback(nint window, nint parameter);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] static extern bool GetLastInputInfo(ref LASTINPUTINFO value);
    [DllImport("kernel32.dll", SetLastError = true)] static extern SafeProcessHandle OpenProcess(uint access, bool inherit, int processId);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern int GetPackageFamilyName(SafeProcessHandle process, ref uint length, StringBuilder? name);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern int GetPackageFullName(SafeProcessHandle process, ref uint length, StringBuilder? name);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern int GetPackagePathByFullName(string name, ref uint length, StringBuilder? path);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] static extern bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, StringBuilder name, ref uint length);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] static extern bool EnumWindows(WindowCallback callback, nint parameter);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(nint window, StringBuilder name, int length);
    [DllImport("user32.dll")] static extern nint GetWindow(nint window, uint command);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] static extern bool PostMessage(nint window, uint message, nint wParam, nint lParam);
}


