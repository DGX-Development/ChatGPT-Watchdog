using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
namespace ChatGPTWatchdog;

// Explicit diagnostics only. These modes never construct TrayApplication or discover ChatGPT.
internal static class ShutdownDiagnostics
{
    static bool Locked() => WindowsTarget.LockState() ?? throw new IOException("Windows session lock state could not be verified.");
    public static int Run(string directory, bool locked)
    {
        Directory.CreateDirectory(directory);
        var log = new ShutdownLog(directory);
        try
        {
            log.Write("SessionLockState", $"source=WTSSessionInfoEx; locked={Locked()}");
            if (locked)
            {
                log.Write("LockedTestScheduled", "Waiting up to five minutes for Windows to lock. Only disposable helper applications will be targeted.");
                var preparation = Stopwatch.StartNew();
                while (!Locked() && preparation.ElapsedMilliseconds < 300000) Thread.Sleep(200);
                if (!Locked()) throw new InvalidOperationException("Locked test not run: Windows was not locked within five minutes.");
                log.Write("WindowsLockDetected", "Starting disposable helper scenarios on the locked workstation.");
            }
            else if (Locked()) throw new InvalidOperationException("Unlocked tests require an unlocked workstation.");
            Case(directory, "running", locked, log);
            Case(directory, "already-exited", locked, log);
            Case(directory, "cancelled", locked, log);
            Case(directory, "native-guard", locked, log);
            if (!locked) IdentityRejection(directory, log);
            log.Write("DiagnosticsPassed", locked ? "Locked helper scenarios passed. This does not verify ChatGPT itself." : "Unlocked helper scenarios passed. Locked tests have not been run.");
            File.WriteAllText(Path.Combine(directory,"result.txt"), locked ? "PASS: actual locked-workstation helper tests; ChatGPT untested." : "PASS: unlocked helper tests; locked workstation and ChatGPT untested.");
            return 0;
        }
        catch (Exception error)
        {
            log.Write("DiagnosticsFailed", error.ToString()); File.WriteAllText(Path.Combine(directory,"result.txt"), "FAIL: " + error); return 1;
        }
    }
    static Process Spawn(string directory, string mode, out string ready)
    {
        string log = Path.Combine(directory, "helper-" + Guid.NewGuid().ToString("N") + ".jsonl"); ready = log + ".ready";
        var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add("--shutdown-helper"); start.ArgumentList.Add(mode); start.ArgumentList.Add(log);
        var child = Process.Start(start) ?? throw new IOException("Helper could not be started.");
        var wait = Stopwatch.StartNew(); while (!File.Exists(ready))
        { if (child.HasExited || wait.ElapsedMilliseconds > 10000) { child.Dispose(); throw new IOException("Helper was not ready."); } Thread.Sleep(40); }
        return child;
    }
    static void Case(string directory, string scenario, bool locked, ShutdownLog log)
    {
        using var child = Spawn(directory, "ignore", out string ready);
        try
        {
            if (Locked() != locked) throw new IOException("Workstation lock state changed before test.");
            var events = new List<string>();
            bool wrote(string name, string detail)
            { events.Add(name); return log.Write(name, $"scenario={scenario}; locked={Locked()}; " + detail); }
            using var target = new NativeShutdownTarget(child.Id, child.StartTime.ToUniversalTime().Ticks, null, Environment.ProcessPath,
                () => Locked() == locked && scenario != "native-guard");
            if (scenario == "already-exited") Cleanup(child, ready);
            using var attempt = new ShutdownAttempt(target, () => Environment.TickCount64,
                () => Locked() == locked && scenario != "cancelled", wrote);
            var watch = Stopwatch.StartNew();
            while (!attempt.Complete && watch.ElapsedMilliseconds < 7000) { attempt.Tick(); Thread.Sleep(50); }
            if (!attempt.Complete || Locked() != locked) throw new IOException("Scenario incomplete or lock state changed.");
            bool expectedSuccess = scenario is "running" or "already-exited";
            if (attempt.Success != expectedSuccess) throw new IOException($"Unexpected result: {scenario}: {attempt.Detail}");
            if (expectedSuccess && !child.WaitForExit(2000)) throw new IOException("Process exit not observed.");
            if (!expectedSuccess && child.HasExited) throw new IOException("Cancelled helper unexpectedly exited.");
            if (events.Count(e => e == "ForcedTerminationAttempted") != (scenario is "running" or "native-guard" ? 1 : 0))
                throw new IOException("Unexpected number of termination attempts.");
            string helperLog = ready[..^6];
            if (File.Exists(helperLog) && File.ReadAllText(helperLog).Contains("WM_CLOSE")) throw new IOException("Direct-close test unexpectedly sent WM_CLOSE.");
            log.Write("ScenarioPassed", $"scenario={scenario}; locked={locked}; {attempt.Detail}");
        }
        finally { Cleanup(child, ready); }
    }
    static void IdentityRejection(string directory, ShutdownLog log)
    {
        using var child = Spawn(directory, "ignore", out string ready);
        try
        {
            bool rejected = false;
            try { using var target = new NativeShutdownTarget(child.Id, child.StartTime.ToUniversalTime().Ticks + 1, null, Environment.ProcessPath); }
            catch (IOException) { rejected = true; }
            if (!rejected || child.HasExited) throw new IOException("Mismatched identity was not rejected safely.");
            log.Write("ScenarioPassed", "Mismatched creation time rejected; helper remains alive.");
        }
        finally { Cleanup(child, ready); }
    }
    static void Cleanup(Process child, string ready)
    {
        if (child.HasExited) return;
        nint window = (nint)long.Parse(File.ReadAllText(ready)); GetWindowThreadProcessId(window, out uint pid);
        if (pid == child.Id) PostMessage(window, 0x8064, 0, 0); // Cooperative cleanup command for our own helper only.
        child.WaitForExit(3000);
    }
    public static void Helper(string mode, string log) => Application.Run(new DisposableWindow(mode, log));
    sealed class DisposableWindow : Form
    {
        readonly string mode, log;
        readonly System.Windows.Forms.Timer safety = new() { Interval = 120000 };
        readonly System.Windows.Forms.Timer delayed = new() { Interval = 4700 };
        public DisposableWindow(string mode, string log)
        {
            this.mode = mode; this.log = log;
            ShowInTaskbar = false; StartPosition = FormStartPosition.Manual; Location = new(-32000, -32000); Size = new(100,100);
            Text = "Disposable watchdog shutdown test";
            safety.Tick += (_, _) => Application.ExitThread(); safety.Start();
            delayed.Tick += (_, _) => { Record("DelayedExit"); Application.ExitThread(); };
            Shown += (_, _) => File.WriteAllText(log + ".ready", Handle.ToInt64().ToString());
        }
        protected override bool ShowWithoutActivation => true;
        void Record(string action) => File.AppendAllText(log, JsonSerializer.Serialize(new { utc = DateTimeOffset.UtcNow, action, locked = Locked() }) + Environment.NewLine);
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x8064) { Record("Cleanup"); Application.ExitThread(); return; }
            if (message.Msg == 0x10)
            {
                Record("WM_CLOSE");
                if (mode == "ignore") { Hide(); return; }
                if (mode == "delayed") { delayed.Start(); return; }
                Record("GracefulExit"); Application.ExitThread(); return;
            }
            base.WndProc(ref message);
        }
        protected override void Dispose(bool disposing) { if (disposing) { safety.Dispose(); delayed.Dispose(); } base.Dispose(disposing); }
    }
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(nint window,out uint pid);
    [DllImport("user32.dll", SetLastError=true)] static extern bool PostMessage(nint window,uint message,nint wParam,nint lParam);
}
