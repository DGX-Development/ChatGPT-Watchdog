using System.Runtime.InteropServices;

namespace ChatGPTWatchdog;

internal static class Appearance
{
    internal static void Apply(Form form, string choice)
    {
        var p = UiColors.From(choice);
        form.BackColor = p.Window;
        void Paint(Control control)
        {
            control.BackColor = control.Parent?.BackColor ?? p.Window;
            control.ForeColor = Equals(control.Tag, "muted") ? p.Muted : p.Text;
            if (control is DangerNotice danger) { danger.SetColors(p.Dark); return; }
            if (control is SurfacePanel panel)
            { panel.BackColor = Equals(panel.Tag, "field") ? p.Window : p.Surface; panel.Border = Equals(panel.Tag, "field") ? p.FieldBorder : p.Border; }
            if (control is ActionButton button)
            {
                bool primary = Equals(button.Tag, "primary");
                button.BackColor = primary ? p.Accent : Equals(button.Tag, "picker") ? p.Window : p.Button;
                button.ForeColor = primary ? p.Dark ? Color.FromArgb(20, 35, 55) : Color.White : p.Text;
                button.Border = primary ? p.Accent : p.FieldBorder; button.Muted = p.Muted;
                if (Equals(button.Tag, "about-close") && !p.Dark && !SystemInformation.HighContrast) button.ForeColor = ColorTranslator.FromHtml("#235993");
            }
            if (Equals(control.Tag, "accent")) control.BackColor = p.Accent;
            if (Equals(control.Tag, "track")) control.BackColor = p.Border;
            if (control is LinkLabel link)
            { link.LinkColor = p.Accent; link.ActiveLinkColor = p.Accent; link.VisitedLinkColor = p.Accent; }
            if (control is ToolStrip strip) MenuStyling.Apply(strip, p);
            if (control is AppearancePicker picker) picker.SetColors(p);
            foreach (Control child in control.Controls) Paint(child);
        }
        foreach (Control child in form.Controls) Paint(child);
        if (form.IsHandleCreated)
        {
            int dark = p.Dark ? 1 : 0, corners = 2;
            DwmSetWindowAttribute(form.Handle, 20, ref dark, sizeof(int));
            DwmSetWindowAttribute(form.Handle, 33, ref corners, sizeof(int));
        }
        form.Invalidate(true);
    }
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(nint handle, int attribute, ref int value, int size);
}

internal abstract class CompactForm : Form
{
    readonly List<Font> fonts = [];
    readonly Icon appIcon = AppBrand.LoadIcon(32);
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string Theme { get; set; } = "System";
    protected CompactForm(string title, Size size)
    {
        Text = title; AutoScaleMode = AutoScaleMode.Dpi;
        Font = OwnFont("Segoe UI Variable Text", 10); ClientSize = size;
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen; Icon = appIcon;
        HandleCreated += (_, _) => RefreshTheme();
    }
    protected Font OwnFont(string family, float size, FontStyle style = FontStyle.Regular)
    { var font = new Font(family, size, style); fonts.Add(font); return font; }
    protected Label TextLabel(string text, int height = 32, bool muted = false) => new()
    { Text = text, Dock = DockStyle.Top, Height = height, Tag = muted ? "muted" : null, Margin = Padding.Empty };
    protected ActionButton MakeButton(string text, bool primary = false) => new()
    {
        Text = text, Size = new(150, 38), Tag = primary ? "primary" : null,
        Font = OwnFont("Segoe UI Variable Text", 10.5f, FontStyle.Bold), Margin = new Padding(8, 0, 0, 0)
    };
    protected TableLayoutPanel Page(string title, string subtitle, params float[] heights) => Page(title, subtitle, false, heights);
    protected TableLayoutPanel Page(string title, string subtitle, bool showBrand, params float[] heights)
    {
        var page = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = heights.Length, Padding = new Padding(28, 0, 28, 0) };
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (float height in heights) page.RowStyles.Add(new RowStyle(height < 0 ? SizeType.Percent : SizeType.Absolute, Math.Abs(height)));
        var header = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
        int textLeft = showBrand ? 64 : 0;
        if (showBrand) header.Controls.Add(new BrandPicture { Bounds = new Rectangle(0, 25, 48, 48), AccessibleName = "ChatGPT Watchdog dog icon" });
        header.Controls.Add(new Label { Text = title, Font = OwnFont("Segoe UI Variable Display", 21, FontStyle.Bold), AutoSize = true, Location = new Point(textLeft, 18) });
        header.Controls.Add(new Label { Text = subtitle, AutoSize = true, Location = new Point(textLeft + 2, 62), Tag = "muted" });
        page.Controls.Add(header, 0, 0); Controls.Add(page); return page;
    }
    protected FlowLayoutPanel Footer() => new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 18, 0, 0), Margin = Padding.Empty };
    public void RefreshTheme()
    {
        Appearance.Apply(this, Theme);
        foreach (var owned in OwnedForms.OfType<CompactForm>()) { owned.Theme = Theme; owned.RefreshTheme(); }
    }
    protected override void Dispose(bool disposing)
    { base.Dispose(disposing); if (disposing) { foreach (var font in fonts) font.Dispose(); appIcon.Dispose(); } }
}

internal sealed class SettingsForm : CompactForm
{
    internal const string IssuesWebsite = "https://github.com/DGX-Development/ChatGPT-Watchdog/issues/new";
    internal Action<string> OpenExternalLink = url => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
    internal readonly NumberField Idle = new(1, 1440, 45);
    internal readonly NumberField Warning = new(10, 600, 120);
    internal readonly NumberField Postpone = new(1, 1440, 60);
    internal readonly AppearancePicker AppearanceField = new() { Tag = "picker" };
    internal readonly MenuStrip NavigationMenu = new() { Dock = DockStyle.Top, Padding = new Padding(15, 4, 0, 4) };
    internal readonly ToolStripMenuItem AppearanceMenu = new("&Appearance");
    readonly ToolStripMenuItem saveMenu = new("&Save settings") { ShortcutKeys = Keys.Control | Keys.S };
    readonly ToolStripMenuItem pauseMenu = new("&Pause monitoring") { ToolTipText = "Pause applies to this run. Restarting Watchdog starts monitoring again." };
    readonly ToolStripMenuItem startupMenu = new("Start Watchdog when I sign in");
    bool monitoringPaused;
    readonly ActionButton saveButton;
    readonly Func<Settings, Settings> save;
    NotificationOptions notificationDraft;
    Settings saved;
    bool savedThisSession;
    internal bool ExitOnClose;
    internal event Action<bool>? PauseRequested;
    internal Func<bool>? IsPaused;
    internal Func<DialogResult>? UnsavedDecision;
    internal bool Dirty { get { try { return Draft() != saved; } catch (InvalidDataException) { return true; } } }

    internal SettingsForm(Settings value, Func<Settings, Settings> save) : base("ChatGPT Watchdog", new(620, 800))
    {
        this.save = save; saved = value; notificationDraft = value.Notifications; Theme = value.Appearance;
        Idle.Value = value.IdleMinutes; Warning.Value = value.WarningSeconds; Postpone.Value = value.PostponeMinutes;
        AppearanceField.Value = value.Appearance;
        var page = Page("ChatGPT Watchdog", "For the ChatGPT Windows desktop app.", true, 102, 156, -100, 76);
        var card = new SurfacePanel { Dock = DockStyle.Fill, Padding = new Padding(24, 18, 24, 18), Margin = Padding.Empty };
        var fields = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Margin = Padding.Empty };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        fields.Controls.Add(new Label { Text = "Monitoring preferences", Font = OwnFont("Segoe UI Variable Display", 14, FontStyle.Bold), Dock = DockStyle.Fill, Margin = Padding.Empty }, 0, 0);
        void Field(string caption, Control input, int row)
        {
            fields.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
            var block = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
            block.Controls.Add(new Label { Text = caption, Dock = DockStyle.Top, Height = 25, Margin = Padding.Empty });
            input.AccessibleName = caption;
            Control field;
            if (input is AppearancePicker picker)
            {
                picker.Padding = new Padding(12, 0, 30, 0);
                field = picker;
            }
            else
            {
                var host = new SurfacePanel { Tag = "field", Radius = 8, Padding = new Padding(12, 5, 6, 5) };
                input.Dock = DockStyle.Fill; host.Controls.Add(input); field = host;
            }
            field.SetBounds(0, 28, Math.Max(1, block.Width), 42);
            field.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            block.Controls.Add(field);
            block.SizeChanged += (_, _) => field.Width = block.ClientSize.Width;
            fields.Controls.Add(block, 0, row);
        }
        Field("Inactivity (minutes)", Idle, 1); Field("Warning (seconds)", Warning, 2);
        Field("Postpone countdown (minutes)", Postpone, 3); Field("Appearance", AppearanceField, 4);
        card.Controls.Add(fields); page.Controls.Add(card, 0, 2);
        var danger = new DangerNotice("This app force-closes ChatGPT",
            "After the inactivity time you set, ChatGPT will be force-closed when the countdown ends, even if it is working on a task.\r\nYou may lose unsaved work. Cancel or postpone the countdown, or pause monitoring, to keep it open.")
        { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 16) };
        page.Controls.Add(danger, 0, 1);
        var footer = Footer(); saveButton = MakeButton("Save settings"); var close = MakeButton("Close");
        saveButton.Click += (_, _) => Save(); close.Click += (_, _) => Close();
        footer.Controls.Add(saveButton); footer.Controls.Add(close); page.Controls.Add(footer, 0, 3);
        CancelButton = close; AcceptButton = saveButton;
        Idle.Changed += Changed; Warning.Changed += Changed; Postpone.Changed += Changed;
        AppearanceField.Changed += () => { Theme = AppearanceField.Value; Changed(); RefreshTheme(); };
        BuildMenu(); Controls.Add(NavigationMenu); MainMenuStrip = NavigationMenu;
        FormClosing += ConfirmClose; Changed();
    }
    Settings Draft() => new(Idle.Value, Warning.Value, Postpone.Value, AppearanceField.Value) { Notifications = notificationDraft, StartWithWindows = saved.StartWithWindows };
    void Changed()
    {
        startupMenu.Checked = saved.StartWithWindows;
        bool editable = !savedThisSession || Dirty;
        saveButton.Text = editable ? "Save settings" : "Saved"; saveButton.Enabled = saveMenu.Enabled = editable;
        foreach (ToolStripMenuItem item in AppearanceMenu.DropDownItems) item.Checked = (string?)item.Tag == AppearanceField.Value;
    }
    internal bool Save()
    {
        try
        {
            var value = Draft(); value.Validate(); saved = save(value); notificationDraft = saved.Notifications; savedThisSession = true;
            Changed(); return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or System.Security.SecurityException)
        {
            using var errorForm = new MessageForm("Settings could not be saved", error.Message, Theme);
            errorForm.ShowDialog(this); return false;
        }
    }
    void BuildMenu()
    {
        NavigationMenu.Font = Font;
        var file = new ToolStripMenuItem("&File");
        saveMenu.Click += (_, _) => Save(); file.DropDownItems.Add(saveMenu);
        file.DropDownItems.Add("&Close to tray", null, (_, _) => Close());
        file.DropDownItems.Add(new ToolStripSeparator());
        var exit = new ToolStripMenuItem("E&xit") { ShortcutKeys = Keys.Control | Keys.Q };
        exit.Click += (_, _) => RequestExit();
        file.DropDownItems.Add(exit);
        var settings = new ToolStripMenuItem("&Settings");
        pauseMenu.Click += (_, _) =>
        {
            bool value = !(IsPaused?.Invoke() ?? monitoringPaused);
            PauseRequested?.Invoke(value); SetMonitoringPaused(value);
        };
        settings.DropDownItems.Add(pauseMenu);
        settings.DropDownItems.Add(new ToolStripSeparator());
        foreach (string choice in new[] { "System", "Light", "Dark" })
        {
            var item = new ToolStripMenuItem(choice) { Tag = choice };
            item.Click += (_, _) => { AppearanceField.Value = choice; Theme = choice; Changed(); RefreshTheme(); };
            AppearanceMenu.DropDownItems.Add(item);
        }
        settings.DropDownItems.Add(AppearanceMenu);
        settings.DropDownOpening += (_, _) => SetMonitoringPaused(IsPaused?.Invoke() ?? monitoringPaused);
        var help = new ToolStripMenuItem("&Help");
        var docs = new ToolStripMenuItem("Help &documentation…") { ShortcutKeys = Keys.F1 };
        docs.Click += (_, _) => ShowDocumentation(false);
        help.DropDownItems.Add(docs);
        var report = new ToolStripMenuItem("Report an &issue")
        { ToolTipText = "Open a new GitHub issue. Review and submit the report yourself." };
        report.Click += (_, _) =>
        {
            try { OpenExternalLink(IssuesWebsite); }
            catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException)
            { using var dialog = new MessageForm("Issue page could not be opened", error.Message, Theme); dialog.ShowDialog(this); }
        };
        help.DropDownItems.Add(report); help.DropDownItems.Add(new ToolStripSeparator());
        help.DropDownItems.Add("&About ChatGPT Watchdog…", null, (_, _) => ShowDocumentation(true));
        settings.DropDownItems.Add(new ToolStripSeparator());
        settings.DropDownItems.Add("&Notifications…", null, (_, _) => ShowNotifications());
        settings.DropDownItems.Add(new ToolStripSeparator());
        startupMenu.Click += (_, _) =>
        {
            try
            {
                saved = save(saved with { StartWithWindows = !saved.StartWithWindows });
                notificationDraft = saved.Notifications; savedThisSession = true; Changed();
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            { using var dialog = new MessageForm("Startup setting could not be saved", error.Message, Theme); dialog.ShowDialog(this); }
        };
        settings.DropDownItems.Add(startupMenu);
        settings.DropDownItems.Add(new ToolStripSeparator());
        settings.DropDownItems.Add("Open &data folder…", null, (_, _) => OpenDataFolder());
        NavigationMenu.Items.AddRange([file, settings, help]);
    }
    internal void ShowNotifications()
    {
        if (OwnedForms.LastOrDefault(f => f.Visible && !f.IsDisposed) is { } existing) { existing.Activate(); return; }
        using var dialog = new NotificationOptionsForm(notificationDraft, Theme, saveOptions: options =>
        {
            // Save only notifications; keep any unsaved timing/appearance fields in their draft.
            var persisted = save(saved with { Notifications = options });
            saved = persisted; notificationDraft = persisted.Notifications; savedThisSession = true; Changed();
            return notificationDraft;
        });
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        notificationDraft = dialog.SelectedOptions; Changed();
    }
    internal void OpenDataFolder()
    {
        try { ApplicationLogs.OpenDataFolder(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        { using var dialog = new MessageForm("Data folder could not be opened", error.Message, Theme); dialog.ShowDialog(this); }
    }
    internal void SetMonitoringPaused(bool value)
    {
        monitoringPaused = value;
        pauseMenu.Text = value ? "&Resume monitoring" : "&Pause monitoring";
        MenuSymbols.Apply(pauseMenu, UiColors.From(Theme).Text);
    }
    internal void RequestExit()
    {
        if (OwnedForms.LastOrDefault(f => f.Visible && !f.IsDisposed) is { } owned) { owned.Activate(); return; }
        ExitOnClose = true; Close(); if (!IsDisposed) ExitOnClose = false;
    }
    internal void ShowDocumentation(bool about)
    {
        if (OwnedForms.LastOrDefault(f => f.Visible && !f.IsDisposed) is { } existing) { existing.Activate(); return; }
        using CompactForm dialog = about ? new AboutForm(Theme) : new HelpForm(Theme);
        dialog.ShowDialog(this);
    }
    void ConfirmClose(object? sender, FormClosingEventArgs e)
    {
        if (e.CloseReason != CloseReason.UserClosing || !Dirty) return;
        DialogResult choice;
        if (UnsavedDecision is not null) choice = UnsavedDecision();
        else { using var dialog = new UnsavedForm(Theme); choice = dialog.ShowDialog(this); }
        if (choice == DialogResult.Yes) e.Cancel = !Save();
        else e.Cancel = choice != DialogResult.No;
    }
}

internal sealed class WarningForm : CompactForm
{
    readonly Label countdown;
    readonly Panel bar;
    readonly int duration;
    readonly NotificationOptions notifications;
    bool attentionRequested;
    bool completed;
    bool cancelQueued;
    public event Action? Canceled;
    public event Action? Postponed;
    internal WarningForm(Settings settings) : base("ChatGPT Watchdog", new(540, 500))
    {
        Theme = settings.Appearance; duration = settings.WarningSeconds; notifications = settings.Notifications;
        TopMost = notifications.AlwaysOnTop; ShowInTaskbar = true;
        var page = Page("ChatGPT will be force-closed", $"Your computer has been inactive for {settings.IdleMinutes} minutes.", 100, -100, 148, 76);
        var card = new SurfacePanel { Dock = DockStyle.Fill, Padding = new Padding(20, 14, 20, 14), Margin = Padding.Empty };
        countdown = TextLabel("02:00", 76); countdown.TextAlign = ContentAlignment.MiddleCenter;
        countdown.Font = OwnFont("Segoe UI Variable Display", 38, FontStyle.Bold);
        var track = new Panel { Dock = DockStyle.Bottom, Height = 5, Tag = "track" };
        bar = new Panel { Dock = DockStyle.Left, Width = 440, Tag = "accent" }; track.Controls.Add(bar);
        track.SizeChanged += (_, _) => bar.Width = track.ClientSize.Width;
        card.Controls.Add(countdown); card.Controls.Add(track); page.Controls.Add(card, 0, 1);
        var danger = new DangerNotice("Force close can lose your work",
            "ChatGPT will be force-closed, even during active work.\r\nUnsaved work may be lost. Cancel or postpone to keep it open.")
        { Dock = DockStyle.Fill, Margin = new Padding(0, 16, 0, 0) };
        page.Controls.Add(danger, 0, 2);
        var footer = Footer(); var cancel = MakeButton("Cancel"); var postpone = MakeButton($"Postpone {settings.PostponeMinutes} min"); postpone.Width = 190;
        cancel.Click += (_, _) => Canceled?.Invoke(); postpone.Click += (_, _) => Postponed?.Invoke();
        CancelButton = cancel; AcceptButton = cancel; footer.Controls.Add(cancel); footer.Controls.Add(postpone); page.Controls.Add(footer, 0, 3);
        FormClosing += (_, e) =>
        {
            if (completed || e.CloseReason != CloseReason.UserClosing) return;
            e.Cancel = true; if (cancelQueued) return; cancelQueued = true;
            BeginInvoke(() => { cancelQueued = false; Canceled?.Invoke(); });
        };
    }
    protected override bool ShowWithoutActivation => true;
    internal void RequestAttention(bool locked, IWarningAttention? attention = null)
    {
        if (attentionRequested || IsDisposed || !Visible) return;
        attentionRequested = true;
        WarningNotification.Request(notifications, locked, attention ?? new WindowsWarningAttention(this));
    }
    public void UpdateRemaining(int seconds)
    {
        seconds = Math.Clamp(seconds, 0, duration); countdown.Text = $"{seconds / 60:00}:{seconds % 60:00}";
        bar.Width = (int)((bar.Parent?.ClientSize.Width ?? 0) * seconds / (double)duration);
        AccessibleDescription = $"ChatGPT will be force-closed in {seconds} seconds. It will be force-closed even if it is working. You may lose unsaved work. Cancel or postpone to keep it open.";
    }
    public void Dismiss() { completed = true; Close(); }
}






