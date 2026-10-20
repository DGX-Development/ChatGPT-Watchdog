namespace ChatGPTWatchdog;

internal enum ProcessObservation { Running, Exited, Unknown }
internal readonly record struct ShutdownResult(bool Success, string Detail);
internal interface IShutdownTarget : IDisposable
{
    string Identity { get; }
    ProcessObservation Observe();
    ShutdownResult Force();
}

// Called only after the inactivity warning expires. No graceful-close or grace-period stage.
internal sealed class ShutdownAttempt : IDisposable
{
    readonly IShutdownTarget target;
    readonly Func<long> clock;
    readonly Func<string, string, bool> write;
    long deadline;
    public bool Complete { get; private set; }
    public bool Success { get; private set; }
    public string Detail { get; private set; } = "Waiting for the force-closed process to exit.";
    public ShutdownAttempt(IShutdownTarget target, Func<long> clock,
        Func<bool> canTerminate, Func<string, string, bool> write)
    {
        this.target = target; this.clock = clock; this.write = write;
        if (!Log("ShutdownConditionDetected", "Warning expired. Direct force close is enabled by design. Unsaved work may be lost.")) return;
        if (!canTerminate()) { Finish(false, "ForceCloseCancelled", "Activity, suspension or monitoring state prevented force close."); return; }
        var state = target.Observe();
        if (state == ProcessObservation.Exited) { Finish(true, "ApplicationExited", "Application already exited. No termination requested."); return; }
        if (state != ProcessObservation.Running) { Finish(false, "IdentityCheckFailed", "Process state could not be verified. No termination requested."); return; }
        if (!Log("ForcedTerminationAttempted", "Countdown expired. Force-closing the verified single process. Unsaved work may be lost.")) return;
        if (!canTerminate()) { Finish(false, "ForceCloseCancelled", "Activity or monitoring state changed before termination."); return; }
        var result = target.Force();
        if (!result.Success) { Finish(false, "ForcedTerminationFailed", result.Detail); return; }
        // This is exit-confirmation time, not a delay before termination.
        deadline = clock() + 5000;
        Log("ForcedTerminationRequested", result.Detail);
    }
    bool Log(string name, string detail)
    {
        if (write(name, target.Identity + "; " + detail)) return true;
        Complete = true; Success = false; Detail = "Force close stopped because its event log could not be written.";
        return false;
    }
    void Finish(bool success, string name, string detail)
    { Success = success; Detail = detail; Complete = true; if (!Log(name, detail)) Success = false; }
    public void Tick()
    {
        if (Complete) return;
        var state = target.Observe();
        if (state == ProcessObservation.Exited)
        { Finish(true, "ForcedTerminationSucceeded", "The monitored process exited after the force-close request."); return; }
        if (state == ProcessObservation.Unknown)
        { Finish(false, "IdentityCheckFailed", "Process exit could not be verified. No further termination requested."); return; }
        if (clock() >= deadline) Finish(false, "ForcedTerminationFailed", "Process exit was not observed within five seconds. No repeat termination requested.");
    }
    public void Cancel(string reason) { if (!Complete) Finish(false, "ShutdownCancelled", reason); }
    public void Dispose() => target.Dispose();
}
