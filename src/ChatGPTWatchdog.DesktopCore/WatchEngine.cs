namespace ChatGPTWatchdog;

internal enum WatchState { Monitoring, Warning, Closing, Failed, Paused, Postponed, Unavailable }

// All times are monotonic milliseconds. This class has no Windows/process side effects.
internal sealed class WatchEngine(Settings settings, long now)
{
    public Settings Settings { get; private set; } = settings;
    public WatchState State { get; private set; } = WatchState.Monitoring;
    public long WarningEnds { get; private set; }
    public long PostponedUntil { get; private set; }
    public string? Target { get; private set; }
    long baseline = now;
    uint? lastInput;
    uint warningInput;
    bool paused;

    public void Reset(long now)
    {
        baseline = now;
        WarningEnds = 0;
        PostponedUntil = 0;
        Target = null;
        State = paused ? WatchState.Paused : WatchState.Monitoring;
    }
    public void Configure(Settings value, long now) { value.Validate(); Settings = value; Reset(now); }
    public void Pause(bool value, long now) { paused = value; Reset(now); }
    public void Cancel(long now) { baseline = now; WarningEnds = 0; State = paused ? WatchState.Paused : WatchState.Monitoring; }
    public void Postpone(long now)
    {
        Cancel(now);
        PostponedUntil = now + Settings.PostponeMinutes * 60_000L;
        State = WatchState.Postponed;
    }
    public void CloseFinished(bool success)
    {
        State = success ? WatchState.Monitoring : WatchState.Failed;
        if (success) Target = null;
    }
    public WatchState Tick(long now, uint input, long idleMilliseconds, string? target,
        bool known = true, bool warningInteraction = false)
    {
        if (paused) return State = WatchState.Paused;
        if (!known || idleMilliseconds < 0)
        {
            Reset(now);
            return State = WatchState.Unavailable;
        }
        bool activity = lastInput.HasValue && input != lastInput;
        // Leave a button interaction queued on the UI thread time to complete first.
        // Do not accept/forget this input: the next sample still cancels if no button was chosen.
        if (State == WatchState.Warning && input != warningInput && warningInteraction)
            return State;
        lastInput = input;
        if (activity)
        {
            baseline = now;
            if (State is WatchState.Warning or WatchState.Failed or WatchState.Unavailable)
                State = WatchState.Monitoring;
        }
        if (target is null)
        {
            Target = null; WarningEnds = 0; baseline = now;
            return State = now < PostponedUntil ? WatchState.Postponed : WatchState.Monitoring;
        }
        // Hiding a window may change the helper-process identity set. That is not permission to retry.
        if (State == WatchState.Failed && !activity) return State;
        if (target != Target)
        {
            Target = target; baseline = now; WarningEnds = 0;
            State = WatchState.Monitoring;
        }
        if (State == WatchState.Closing) return State;
        if (State == WatchState.Failed) return State;
        if (now < PostponedUntil) return State = WatchState.Postponed;
        long idle = Math.Min(idleMilliseconds, now - baseline);
        if (State == WatchState.Warning)
        {
            if (input != warningInput || idle < Settings.IdleMinutes * 60_000L)
                return State = WatchState.Monitoring;
            if (now >= WarningEnds) return State = WatchState.Closing;
            return State;
        }
        State = WatchState.Monitoring;
        if (idle >= Settings.IdleMinutes * 60_000L)
        {
            warningInput = input;
            WarningEnds = now + Settings.WarningSeconds * 1000L;
            State = WatchState.Warning;
        }
        return State;
    }
    // Sleep never consumes a warning countdown. If already overdue, give a full new warning.
    public void Resume(long now)
    {
        if (State == WatchState.Closing) { State = WatchState.Failed; return; }
        if (State == WatchState.Warning)
        { State = WatchState.Warning; WarningEnds = now + Settings.WarningSeconds * 1000L; }
    }
}


