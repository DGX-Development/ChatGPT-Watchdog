namespace ChatGPTWatchdog;

internal sealed class MessageForm : CompactForm
{
    internal MessageForm(string title, string message, string theme) : base(title, new(540, 300))
    {
        Theme = theme; ShowInTaskbar = false; StartPosition = FormStartPosition.CenterParent;
        var page = Page(title, "", 84, -100, 76);
        var body = new Label { Text = message, Dock = DockStyle.Fill, Padding = new Padding(0, 6, 0, 0), Margin = Padding.Empty };
        page.Controls.Add(body, 0, 1); var footer = Footer(); var close = MakeButton("Close");
        close.Click += (_, _) => Close(); footer.Controls.Add(close); page.Controls.Add(footer, 0, 2); CancelButton = close;
    }
}

internal sealed class UnsavedForm : CompactForm
{
    internal UnsavedForm(string theme) : base("Unsaved changes", new(580, 300))
    {
        Theme = theme; ShowInTaskbar = false; StartPosition = FormStartPosition.CenterParent;
        var page = Page("Unsaved changes", "Choose what to do with your settings edits.", 102, -100, 76);
        page.Controls.Add(new Label { Text = "Save your changes before closing, or discard them to keep\r\nyour previously saved settings.", Dock = DockStyle.Fill, Padding = new Padding(0, 10, 0, 0) }, 0, 1);
        var footer = Footer();
        foreach (var (caption, result) in new[] { ("Keep editing", DialogResult.Cancel), ("Discard changes", DialogResult.No), ("Save changes", DialogResult.Yes) })
        {
            var button = MakeButton(caption); button.Width = 162; button.DialogResult = result;
            if (result == DialogResult.Cancel) CancelButton = button;
            footer.Controls.Add(button);
        }
        page.Controls.Add(footer, 0, 2);
    }
}

internal sealed class AboutForm : CompactForm
{
    internal const string DeveloperWebsite = "https://dgx.digitalguy.me";
    internal AboutForm(string theme) : base("About ChatGPT Watchdog", new(640, 570))
    {
        Theme = theme; ShowInTaskbar = false; StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable; MaximizeBox = true;
        Size = new Size(640, 570); MinimumSize = Size;
        var page = Page("About ChatGPT Watchdog", "For the ChatGPT Windows desktop app.", 104, -100, 78);
        var header = (Panel)page.GetControlFromPosition(0, 0)!;
        var headings = header.Controls.OfType<Label>().ToArray();
        headings[0].AutoSize = false; headings[0].Bounds = new Rectangle(0, 20, 560, 38);
        headings[1].AutoSize = false; headings[1].Bounds = new Rectangle(2, 65, 560, 28);
        foreach (var heading in headings) heading.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        var card = new SurfacePanel { Dock = DockStyle.Fill, Padding = new Padding(24), Margin = Padding.Empty };
        string version = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";
        var small = OwnFont("Segoe UI Variable Text", 9);
        card.Controls.Add(new BrandPicture { Bounds = new Rectangle(24, 22, 64, 64), AccessibleName = "ChatGPT Watchdog dog icon" });
        card.Controls.Add(new Label { Text = "ChatGPT Watchdog", Font = OwnFont("Segoe UI Variable Display", 14, FontStyle.Bold), Bounds = new Rectangle(106, 26, 390, 30) });
        card.Controls.Add(new Label { Text = $"Version {version}", Tag = "muted", Bounds = new Rectangle(107, 60, 390, 25) });
        card.Controls.Add(new Label { Text = "A watchdog for the ChatGPT Windows desktop app.\r\n\r\nCreated by DGX Development.", Bounds = new Rectangle(24, 110, 490, 82) });
        card.Controls.Add(new Label { Text = "Licensed under PolyForm Noncommercial 1.0.0.\r\nFull license included as LICENSE.txt.", Tag = "muted", Font = small, Bounds = new Rectangle(24, 200, 490, 52) });
        card.Controls.Add(new Label { Text = "Developer Website", Tag = "muted", Font = small, Bounds = new Rectangle(24, 268, 450, 22) });
        var website = new LinkLabel { Text = "DGX.digitalguy.me", AccessibleName = "Developer Website", Bounds = new Rectangle(24, 294, 450, 26), UseMnemonic = false };
        website.LinkClicked += (_, _) =>
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(DeveloperWebsite) { UseShellExecute = true }); }
            catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException)
            { using var dialog = new MessageForm("Website could not be opened", error.Message, Theme); dialog.ShowDialog(this); }
        };
        card.Controls.Add(website); page.Controls.Add(card, 0, 1);
        var footer = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
        var close = MakeButton("Close"); close.Tag = "about-close"; close.Size = new Size(110, 38); close.Top = 20;
        close.Click += (_, _) => Close(); footer.Controls.Add(close);
        footer.SizeChanged += (_, _) => close.Left = Math.Max(0, footer.ClientSize.Width - close.Width);
        page.Controls.Add(footer, 0, 2); CancelButton = close;
    }
}

internal sealed class HelpForm : CompactForm
{
    internal static readonly (string Title, string Body)[] Topics =
    [
        ("Getting started", "ChatGPT Watchdog helps prevent the ChatGPT Windows desktop app being left running after you walk away. It does not close ChatGPT in a web browser.\r\n\r\nDefaults: 45 minutes of computer inactivity, a 120-second warning, and 60-minute postponement. Review your settings and choose Save settings.\r\n\r\nClosing settings leaves monitoring running. Double-click the dog tray icon to reopen them. Use Pause monitoring or File > Exit when unattended work must continue."),
        ("Inactivity and warnings", "Keyboard or mouse input anywhere in your Windows session resets the timer. Reading, watching or leaving a task running without input counts as inactivity. Watchdog does not detect active work.\r\n\r\nWhen the countdown ends, ChatGPT is force-closed, even during ongoing work. Unsaved work may be lost. Cancel, postpone or resume input before it ends to keep ChatGPT open. Closing the warning or pressing Escape cancels it and starts a fresh inactivity period."),
        ("Postpone and pause", "Postpone is available on the countdown. It prevents closure for the saved interval; afterward, the inactivity threshold must also be met before a new warning.\r\n\r\nPause monitoring is available in Settings and the tray menu. While paused, it changes to Resume monitoring, the tray heading reads MONITORING PAUSED and the dog gains an amber dot. Resume starts a fresh inactivity period.\r\n\r\nPause and postponement last only for the current run. Restarting Watchdog starts monitoring again."),
        ("Locking and sleep", "While Windows is locked, monitoring continues and ChatGPT can be force-closed. The warning cannot appear on the lock screen; sound and window attention are skipped. Unlocking cancels a pending warning.\r\n\r\nWatchdog cannot run while the computer is asleep or hibernating and does not wake it. After resume, an expired warning receives a fresh countdown."),
        ("Settings and notifications", "Save settings applies timing and appearance changes. Saved means there are no further edits to save. Choose System, Light or Dark under Settings > Appearance.\r\n\r\nSettings > Notifications controls sound, a custom WAV file, bringing the warning forward and always-on-top. Preview tests sound without saving or starting a countdown. Save options applies notifications immediately; unsaved main-window edits stay unsaved.\r\n\r\nWindows may refuse foreground activation; the taskbar flashes briefly instead. Volume and mute settings apply. Missing or unplayable custom audio falls back to Windows sound."),
        ("Automatic startup", "Start Watchdog when I sign in defaults to On. Save settings once to register a new portable copy. The Settings menu startup switch applies immediately. Windows startup controls may disable or delay launch.\r\n\r\nKeep the app in a permanent folder. Turn startup off before moving it, then enable it from the new location. File > Exit stops the current run; startup remains enabled for the next sign-in.\r\n\r\nLaunching Watchdog again opens the existing settings window. Windows may hide its brief startup notification."),
        ("Data and updates", "Settings > Open data folder opens %LOCALAPPDATA%\\ChatGPT Watchdog. It contains settings.json, imported sounds and shutdown logs. Custom sound originals are left unchanged. Exit Watchdog before manually copying or editing its settings.\r\n\r\nTo update, exit Watchdog and replace the supplied application files in the same folder. Keep your separate data folder. This build requires the .NET 10 Desktop Runtime.\r\n\r\nTo remove Watchdog, turn off startup, exit, then remove the application folder. The data folder can be retained or deleted separately."),
        ("Troubleshooting", "Hover over the tray icon or open its menu to check status. Monitoring unavailable means a reliable application identity or input reading could not be obtained; no force-close request is made.\r\n\r\nClosure not confirmed means ChatGPT or related processes may still be running. Use Settings > Open data folder, then open its logs subfolder to inspect the shutdown log. Watchdog does not repeatedly retry a failed closure while unattended. Help > Report an issue opens this project's GitHub issue form. Review and submit the report yourself; omit private paths and data.\r\n\r\nWatchdog targets the identified desktop app in your Windows session. It does not sign you out of ChatGPT or close every process with a matching name.")
    ];
    internal readonly ListBox TopicList = new() { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 42, IntegralHeight = false };
    readonly Label topicTitle;
    readonly RichTextBox content;
    internal HelpForm(string theme) : base("ChatGPT Watchdog Help", new(820, 700))
    {
        Theme = theme; ShowInTaskbar = false; StartPosition = FormStartPosition.CenterParent;
        var page = Page("ChatGPT Watchdog Help", "Monitoring, warnings, and settings.", 102, 140, -100, 76);
        page.Controls.Add(new DangerNotice("This app force-closes ChatGPT",
            "When the inactivity warning expires, ChatGPT is force-closed, even if it is working on a task. You may lose unsaved work.\r\nCancel or postpone the countdown, or pause monitoring before leaving work unattended.")
        { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 16) }, 0, 1);
        var columns = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240)); columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var navigation = new SurfacePanel { Dock = DockStyle.Fill, Padding = new Padding(8), Margin = new Padding(0, 0, 14, 0) };
        var body = new SurfacePanel { Dock = DockStyle.Fill, Padding = new Padding(24, 20, 24, 20), Margin = Padding.Empty };
        TopicList.AccessibleName = "Help topics"; TopicList.Items.AddRange(Topics.Select(t => t.Title).ToArray()); navigation.Controls.Add(TopicList);
        topicTitle = TextLabel("Getting started", 38); topicTitle.Font = OwnFont("Segoe UI Variable Display", 14, FontStyle.Bold);
        content = new RichTextBox { DetectUrls = false, Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.None, ScrollBars = RichTextBoxScrollBars.Vertical, WordWrap = true, AccessibleName = "Help topic text" }; body.Controls.Add(content); body.Controls.Add(topicTitle);
        TopicList.DrawItem += (_, e) =>
        {
            if (e.Index < 0) return; var p = UiColors.From(Theme); bool selected = (e.State & DrawItemState.Selected) != 0;
            using var fill = new SolidBrush(selected ? p.Hover : p.Surface); e.Graphics.FillRectangle(fill, e.Bounds);
            TextRenderer.DrawText(e.Graphics, TopicList.Items[e.Index]?.ToString(), Font, Rectangle.Inflate(e.Bounds, -8, 0), p.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            e.DrawFocusRectangle();
        };
        TopicList.SelectedIndexChanged += (_, _) => { if (TopicList.SelectedIndex >= 0) { var topic = Topics[TopicList.SelectedIndex]; topicTitle.Text = topic.Title; content.Text = topic.Body; content.Select(0, 0); content.ScrollToCaret(); } };
        TopicList.SelectedIndex = 0; columns.Controls.Add(navigation, 0, 0); columns.Controls.Add(body, 1, 0); page.Controls.Add(columns, 0, 2);
        var footer = Footer(); var close = MakeButton("Close"); close.Click += (_, _) => Close(); footer.Controls.Add(close); page.Controls.Add(footer, 0, 3); CancelButton = close;
    }
}
