namespace ChatGPTWatchdog;

// Shared by the real tray and isolated previews; constructing this menu never starts monitoring.
internal sealed class MonitoringStatusItem(string text) : ToolStripMenuItem(text)
{ public override bool CanSelect => false; }

internal sealed class TrayMenu : ContextMenuStrip
{
    internal string StatusText = "Waiting for ChatGPT";
    internal readonly MonitoringStatusItem StatusHeading = new("MONITORING ACTIVE")
    { Enabled = false, Tag = "status-active" };
    readonly ToolStripSeparator statusSeparator = new();
    readonly Font headingFont;
    internal readonly ToolStripMenuItem Pause = new("Pause monitoring") { ToolTipText = "Pause applies to this run. Restarting Watchdog starts monitoring again." };
    internal TrayMenu(Action settings, Action togglePause, Action<bool> documentation, Action exit)
    {
        headingFont = new Font(Font.FontFamily, Font.Size + .5f, FontStyle.Bold); StatusHeading.Font = headingFont;
        Items.Add(StatusHeading); Items.Add(statusSeparator);
        Pause.Click += (_, _) => togglePause(); Items.Add(Pause);
        Items.Add("Settings…", null, (_, _) => settings());
        Items.Add(new ToolStripSeparator());
        Items.Add("Help documentation…", null, (_, _) => documentation(false));
        Items.Add("About ChatGPT Watchdog…", null, (_, _) => documentation(true));
        Items.Add(new ToolStripSeparator());
        Items.Add("Exit watchdog", null, (_, _) => exit());
    }
    internal void Update(WatchState state, bool paused, bool hasTarget, long postponedMinutes, UiColors colors)
    {
        StatusText = paused ? "Monitoring paused" : state switch
        {
            WatchState.Failed => "Shutdown incomplete",
            WatchState.Unavailable => "Monitoring unavailable",
            WatchState.Postponed => $"Postponed for {postponedMinutes} min",
            WatchState.Warning => "Force-close countdown",
            WatchState.Closing => "Verifying force close",
            _ => hasTarget ? "Monitoring active" : "Waiting for ChatGPT"
        };
        (string heading, string symbol) = paused ? ("MONITORING PAUSED", "status-paused") : state switch
        {
            WatchState.Warning => ("FORCE-CLOSE COUNTDOWN", "status-warning"),
            WatchState.Closing => ("VERIFYING FORCE CLOSE", "status-warning"),
            WatchState.Failed => ("CLOSURE NOT CONFIRMED", "status-error"),
            WatchState.Unavailable => ("MONITORING UNAVAILABLE", "status-error"),
            WatchState.Postponed => ("MONITORING POSTPONED", "status-postponed"),
            _ => ("MONITORING ACTIVE", "status-active")
        };
        if (StatusHeading.Text != heading || !Equals(StatusHeading.Tag, symbol))
        {
            StatusHeading.Text = heading; StatusHeading.Tag = symbol;
            MenuStyling.Apply(this, colors);
        }
        string command = paused ? "Resume monitoring" : "Pause monitoring";
        if (Pause.Text != command) { Pause.Text = command; MenuSymbols.Apply(Pause, colors.Text); }
    }
    protected override void Dispose(bool disposing)
    { base.Dispose(disposing); if (disposing) headingFont.Dispose(); }
}
