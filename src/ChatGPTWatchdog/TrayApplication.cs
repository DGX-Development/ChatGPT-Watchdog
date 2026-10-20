using Microsoft.Win32;

namespace ChatGPTWatchdog;

internal sealed class TrayApplication : ApplicationContext
{
    readonly SettingsStore store;
    readonly WatchEngine engine;
    readonly NotifyIcon icon;
    readonly Icon trayIcon = AppBrand.LoadIcon(SystemInformation.SmallIconSize.Width);
    readonly TrayMenu menu;
    readonly SingleInstanceChannel channel;
    readonly System.Windows.Forms.Timer timer = new() { Interval = 1000 };
    readonly Icon pausedTrayIcon = AppBrand.LoadPausedIcon(SystemInformation.SmallIconSize.Width);
    bool paused;
    SettingsForm? settings;
    WarningForm? warning;
    bool locked;
    bool suspended;
    readonly ShutdownLog log = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChatGPT Watchdog", "logs"));
    long lastTick = Environment.TickCount64;
    long interactionUntil;
    InputSample? priorInput;
    string? failure;
    ShutdownAttempt? shutdown;

    public TrayApplication(SettingsStore store, Settings value)
    {
        this.store = store;
        engine = new(value, Environment.TickCount64);
        menu = new TrayMenu(ShowSettings, () => SetPaused(!paused), ShowDocumentation,
            () => { if (settings is not null) settings.RequestExit(); else ExitThread(); });
        _ = menu.Handle; // Create the UI dispatch handle before subscribing to SystemEvents.
        icon = new NotifyIcon { Text = "ChatGPT Watchdog: Force-close monitoring", Icon = trayIcon, Visible = true, ContextMenuStrip = menu };
        icon.DoubleClick += (_, _) => ShowSettings();
        MenuStyling.Apply(menu, UiColors.From(value.Appearance));
        UpdateStatus();
        locked = WindowsTarget.Locked();
        SystemEvents.SessionSwitch += SessionChanged;
        SystemEvents.PowerModeChanged += PowerChanged;
        SystemEvents.UserPreferenceChanged += PreferencesChanged;
        timer.Tick += (_, _) => Tick(); timer.Start();
        // A visible first-launch settings window explains the utility before any real idle period.
        if (!store.Exists) ShowSettings();
        channel = new SingleInstanceChannel(() => OnUi(ShowSettings));
        OnUi(() => icon.ShowBalloonTip(8000, "Watchdog started", "Monitoring active.", ToolTipIcon.Info));
    }
    void ShowSettings()
    {
        engine.Cancel(Environment.TickCount64); DismissWarning();
        if (settings is not null)
        { (settings.OwnedForms.LastOrDefault(f => f.Visible && !f.IsDisposed) ?? settings).Activate(); return; }
        settings = new SettingsForm(engine.Settings, value =>
        {
            using var startup = StartupRegistration.Begin(value.StartWithWindows, Environment.ProcessPath ?? "");
            var persisted = store.Save(value); startup.Commit(); engine.Configure(persisted, Environment.TickCount64);
            MenuStyling.Apply(menu, UiColors.From(persisted.Appearance));
            return persisted;
        });
        var window = settings;
        window.IsPaused = () => paused;
        window.SetMonitoringPaused(paused);
        window.PauseRequested += SetPaused;
        window.FormClosed += (_, _) =>
        {
            settings = null; engine.Cancel(Environment.TickCount64);
            MenuStyling.Apply(menu, UiColors.From(engine.Settings.Appearance));
            if (window.ExitOnClose) ExitThread();
        };
        settings.Show();
    }
    void ShowDocumentation(bool about)
    {
        // Reuse the settings window as owner; no independent help window or duplicate taskbar entry.
        ShowSettings();
        settings?.ShowDocumentation(about);
    }
    void ReportShutdownFailure()
    {
        string message = "ChatGPT may still be running. Use Settings > Open data folder, then logs, for details. " + failure;
        icon.ShowBalloonTip(10000, "ChatGPT closure not confirmed", message.Length > 250 ? message[..250] : message, ToolTipIcon.Warning);
    }
    void Tick()
    {
        if (suspended) return;
        long now = Environment.TickCount64;
        if (now - lastTick > 5000) engine.Resume(now);
        lastTick = now;
        if (shutdown is not null && engine.State != WatchState.Closing) StopShutdown("Monitoring state changed.");
        if (engine.State == WatchState.Closing)
        {
            if (shutdown is null) { engine.CloseFinished(false); UpdateStatus(); return; }
            shutdown.Tick();
            if (shutdown.Complete)
            {
                bool success = shutdown.Success;
                failure = success ? null : shutdown.Detail;
                if (success)
                {
                    var remaining = WindowsTarget.Read();
                    if (!remaining.Known || remaining.ProcessIds.Count != 0)
                    {
                        success = false; failure = "The monitored process exited, but other ChatGPT processes remain or their state is unknown. No additional processes were terminated.";
                        log.Write("RelatedProcessesRemain", remaining.Error ?? $"count={remaining.ProcessIds.Count}; no process-tree termination");
                    }
                }
                engine.CloseFinished(success);
                if (!success) ReportShutdownFailure();
                shutdown.Dispose(); shutdown = null;
            }
            UpdateStatus(); return;
        }
        var input = WindowsTarget.Input();
        var target = WindowsTarget.Read();
        // Give a local warning-button click a short chance to finish before automatic cancellation.
        if (warning is not null && input.HasValue && priorInput.HasValue && input.Value.Token != priorInput.Value.Token &&
            (warning.ContainsFocus || warning.Bounds.Contains(Cursor.Position))) interactionUntil = now + 600;
        priorInput = input;
        var previousState = engine.State;
        var state = engine.Tick(now, input?.Token ?? 0, input?.IdleMilliseconds ?? -1,
            target.Key, target.Known && input.HasValue, now < interactionUntil);
        if (state == WatchState.Unavailable && previousState != WatchState.Unavailable)
            log.Write("MonitoringUnavailable", target.Error ?? "Windows input reading unavailable. No close request or escalation.");
        if (state == WatchState.Warning)
        {
            if (warning is null && !locked)
            {
                warning = new WarningForm(engine.Settings);
                warning.Canceled += () => { engine.Cancel(Environment.TickCount64); DismissWarning(); };
                warning.Postponed += () => { engine.Postpone(Environment.TickCount64); DismissWarning(); };
                warning.Show();
                warning.RequestAttention(locked || WindowsTarget.Locked());
            }
            warning?.UpdateRemaining((int)Math.Ceiling((engine.WarningEnds - now) / 1000.0));
        }
        else DismissWarning();
        if (state == WatchState.Closing)
        {
            // Resample after discovery. Never act on an earlier mouse/keyboard sample.
            var fresh = WindowsTarget.Input();
            if (!fresh.HasValue || !input.HasValue || fresh.Value.Token != input.Value.Token)
            { engine.Cancel(now); UpdateStatus(); return; }
            IShutdownTarget? captured = null;
            try
            {
                captured = WindowsTarget.Capture(target, () => !suspended && engine.State == WatchState.Closing && WindowsTarget.Input()?.Token == fresh.Value.Token);
                if (WindowsTarget.Input()?.Token != fresh.Value.Token) { captured.Dispose(); engine.Cancel(now); return; }
                shutdown = new ShutdownAttempt(captured, () => Environment.TickCount64,
                    () => !suspended && engine.State == WatchState.Closing && WindowsTarget.Input()?.Token == fresh.Value.Token, log.Write);
            }
            catch (Exception error) when (error is IOException or System.ComponentModel.Win32Exception or UnauthorizedAccessException)
            {
                captured?.Dispose(); engine.CloseFinished(false); failure = error.Message;
                log.Write("ShutdownConditionDetected", "Target capture failed."); log.Write("ForcedTerminationFailed", error.ToString());
                ReportShutdownFailure();
            }
        }
        UpdateStatus();
    }
    void SetPaused(bool value)
    {
        paused = value; engine.Pause(value, Environment.TickCount64);
        StopShutdown("Monitoring pause state changed."); DismissWarning();
        settings?.SetMonitoringPaused(value); UpdateStatus();
    }
    void UpdateStatus()
    {
        menu.Update(engine.State, paused, engine.Target is not null,
            Math.Max(1, (long)Math.Ceiling((engine.PostponedUntil - Environment.TickCount64) / 60_000.0)),
            UiColors.From(engine.Settings.Appearance));
        var desiredIcon = paused ? pausedTrayIcon : trayIcon;
        if (!ReferenceEquals(icon.Icon, desiredIcon)) icon.Icon = desiredIcon;
        string text = "ChatGPT Watchdog: " + menu.StatusText;
        icon.Text = text.Length > 63 ? text[..63] : text;
    }
    void StopShutdown(string reason)
    { shutdown?.Cancel(reason); shutdown?.Dispose(); shutdown = null; }
    void DismissWarning()
    {
        var form = warning; warning = null;
        if (form is not null) { form.Dismiss(); form.Dispose(); }
    }
    void OnUi(Action action)
    {
        // SystemEvents callbacks arrive on their own thread; the menu supplies a UI handle.
        try { if (menu.IsHandleCreated && !menu.IsDisposed) menu.BeginInvoke(action); }
        catch (InvalidOperationException) { /* Shutdown can race with an already queued system event. */ }
    }
    void SessionChanged(object? sender, SessionSwitchEventArgs e) => OnUi(() =>
    {
        if (e.Reason is SessionSwitchReason.SessionLock or SessionSwitchReason.ConsoleDisconnect or SessionSwitchReason.RemoteDisconnect)
        { locked = true; DismissWarning(); }
        else if (e.Reason is SessionSwitchReason.SessionUnlock or SessionSwitchReason.SessionLogon or SessionSwitchReason.ConsoleConnect or SessionSwitchReason.RemoteConnect)
        { locked = false; StopShutdown("Windows unlocked."); engine.Cancel(Environment.TickCount64); DismissWarning(); }
    });
    void PowerChanged(object? sender, PowerModeChangedEventArgs e) => OnUi(() =>
    {
        if (e.Mode == PowerModes.Suspend) { StopShutdown("Computer suspended."); suspended = true; DismissWarning(); }
        if (e.Mode == PowerModes.Resume)
        { suspended = false; engine.Resume(Environment.TickCount64); DismissWarning(); }
    });
    void PreferencesChanged(object? sender, UserPreferenceChangedEventArgs e) => OnUi(() =>
    { settings?.RefreshTheme(); warning?.RefreshTheme(); MenuStyling.Apply(menu, UiColors.From(engine.Settings.Appearance)); });
    protected override void ExitThreadCore()
    {
        channel.Dispose(); StopShutdown("Watchdog exited."); timer.Stop(); timer.Dispose();
        SystemEvents.SessionSwitch -= SessionChanged;
        SystemEvents.PowerModeChanged -= PowerChanged;
        SystemEvents.UserPreferenceChanged -= PreferencesChanged;
        DismissWarning(); settings?.Close(); settings?.Dispose();
        icon.Visible = false; icon.Dispose(); trayIcon.Dispose(); pausedTrayIcon.Dispose(); menu.Dispose();
        base.ExitThreadCore();
    }
}






