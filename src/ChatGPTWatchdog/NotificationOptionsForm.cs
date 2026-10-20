using System.ComponentModel;

namespace ChatGPTWatchdog;

internal sealed class OptionToggle : ActionButton
{
    bool on;
    internal event Action? Changed;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal bool IsOn
    {
        get => on;
        set { on = value; Text = value ? "On" : "Off"; AccessibleDescription = Text; Invalidate(); }
    }
    internal OptionToggle(bool value)
    {
        IsOn = value; AccessibleRole = AccessibleRole.CheckButton;
        Click += (_, _) => { IsOn = !IsOn; Changed?.Invoke(); };
    }
}

internal sealed class NotificationOptionsForm : CompactForm
{
    internal readonly OptionToggle Sound;
    internal readonly OptionToggle Foreground;
    internal readonly OptionToggle OnTop;
    internal readonly AppearancePicker SoundChoice = new("Windows sound", "Custom WAV") { Tag = "picker" };
    internal readonly TextBox SoundPath = new() { BorderStyle = BorderStyle.None, Dock = DockStyle.Fill, AccessibleName = "Custom WAV file" };
    internal readonly Label Hint = new() { Dock = DockStyle.Fill, Tag = "muted", Margin = Padding.Empty };
    internal readonly ActionButton PreviewSound;
    readonly ActionButton browse;
    readonly Func<string?, bool> play;
    internal NotificationOptions SelectedOptions { get; private set; }
    internal NotificationOptionsForm(NotificationOptions options, string theme, Func<string?, bool>? preview = null, Func<NotificationOptions, NotificationOptions>? saveOptions = null)
        : base("Notification options", new(620, 720))
    {
        Theme = theme; SelectedOptions = options; play = preview ?? WindowsWarningAttention.Play;
        ShowInTaskbar = false; StartPosition = FormStartPosition.CenterParent;
        Sound = new(options.SoundEnabled); Foreground = new(options.BringToForeground); OnTop = new(options.AlwaysOnTop);
        SoundChoice.Value = options.SoundPath is null ? "Windows sound" : "Custom WAV";
        SoundPath.Text = options.SoundPath ?? ""; SoundPath.PlaceholderText = "Custom WAV file";
        var page = Page("Notifications", "Save options applies these notification settings immediately.", 92, -100, 76);
        var card = new SurfacePanel { Dock = DockStyle.Fill, Padding = new Padding(22), Margin = Padding.Empty };
        var fields = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 9, Margin = Padding.Empty };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (int height in new[] { 52, 52, 56, 52, 72, 30, 52, 52, 0 })
            fields.RowStyles.Add(new RowStyle(height == 0 ? SizeType.Percent : SizeType.Absolute, height == 0 ? 100 : height));
        void ToggleRow(string label, OptionToggle toggle, int row)
        {
            var host = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            host.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86));
            host.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var text = new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 0, 16, 0) };
            toggle.Dock = DockStyle.Fill; toggle.Margin = new Padding(0, 8, 0, 8); toggle.AccessibleName = label;
            host.Controls.Add(text, 0, 0); host.Controls.Add(toggle, 1, 0); fields.Controls.Add(host, 0, row);
        }
        ToggleRow("Play warning sound", Sound, 0);
        SoundChoice.Dock = DockStyle.Fill; SoundChoice.Margin = new Padding(0, 6, 0, 6);
        SoundChoice.Padding = new Padding(12, 0, 30, 0); SoundChoice.AccessibleName = "Warning sound";
        fields.Controls.Add(SoundChoice, 0, 1);
        var fileRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0, 4, 0, 12) };
        fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
        fileRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var file = new SurfacePanel { Tag = "field", Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(10, 10, 10, 6) };
        file.Controls.Add(SoundPath); browse = MakeButton("Browse"); browse.AccessibleName = "Browse for WAV sound"; browse.MinimumSize = new Size(96, 36); browse.Dock = DockStyle.Fill; browse.Margin = new Padding(12, 0, 0, 0);
        fileRow.Controls.Add(file, 0, 0); fileRow.Controls.Add(browse, 1, 0); fields.Controls.Add(fileRow, 0, 2);
        var previewRow = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
        PreviewSound = MakeButton("Preview sound"); PreviewSound.SetBounds(0, 6, 150, 38);
        previewRow.Controls.Add(PreviewSound); fields.Controls.Add(previewRow, 0, 3);
        Hint.Margin = new Padding(0, 2, 0, 12); fields.Controls.Add(Hint, 0, 4);
        fields.Controls.Add(new Label { Text = "Warning window", Font = OwnFont("Segoe UI Variable Text", 11, FontStyle.Bold), Dock = DockStyle.Fill, Margin = Padding.Empty }, 0, 5);
        ToggleRow("Bring warning to foreground", Foreground, 6);
        ToggleRow("Always on top", OnTop, 7);
        fields.Controls.Add(new Label { Text = "Windows may refuse foreground activation; Watchdog requests a brief taskbar flash instead.\r\nWarnings cannot appear over the Windows lock screen. Audio follows Windows volume and mute settings.", Dock = DockStyle.Fill, Tag = "muted", Margin = new Padding(0, 10, 0, 0) }, 0, 8);
        card.Controls.Add(fields); page.Controls.Add(card, 0, 1);
        var footer = Footer(); var use = MakeButton("Save options"); var cancel = MakeButton("Cancel");
        use.Click += (_, _) =>
        {
            if (Sound.IsOn && SoundChoice.Value == "Custom WAV" && string.IsNullOrWhiteSpace(SoundPath.Text))
            { Hint.Text = "Choose a .wav file, or select Windows sound."; return; }
            var draft = Draft();
            if (draft.SoundEnabled && draft.SoundPath is not null && WarningSound.Problem(draft.SoundPath) is string problem)
            { Hint.Text = problem; return; }
            try { SelectedOptions = saveOptions?.Invoke(draft) ?? draft; }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or System.Security.SecurityException)
            { Hint.Text = "Options could not be saved. " + error.Message; return; }
            DialogResult = DialogResult.OK; Close();
        };
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        footer.Controls.Add(use); footer.Controls.Add(cancel); page.Controls.Add(footer, 0, 2);
        AcceptButton = use; CancelButton = cancel;
        Sound.Changed += RefreshSound; SoundChoice.Changed += RefreshSound; SoundPath.TextChanged += (_, _) => RefreshSound();
        browse.Click += (_, _) =>
        {
            using var picker = CreateSoundPicker(SoundPath.Text);
            if (picker.ShowDialog(this) == DialogResult.OK) SoundPath.Text = picker.FileName;
        };
        PreviewSound.Click += (_, _) => PreviewSelection(); RefreshSound();
    }
    internal NotificationOptions Draft() => new(Sound.IsOn,
        SoundChoice.Value == "Custom WAV" && !string.IsNullOrWhiteSpace(SoundPath.Text) ? SoundPath.Text.Trim() : null, Foreground.IsOn, OnTop.IsOn);
    void RefreshSound()
    {
        bool custom = SoundChoice.Value == "Custom WAV";
        SoundChoice.Enabled = PreviewSound.Enabled = Sound.IsOn;
        SoundPath.Enabled = browse.Enabled = custom && Sound.IsOn;
        Hint.Text = custom ? "Choose a PCM WAV file. It is copied into Watchdog's local data folder when Save options is clicked. The original stays unchanged."
            : "Uses the Windows Information sound once when the countdown appears. Preview does not save settings or start a countdown.";
    }
    internal void PreviewSelection()
    {
        if (!Sound.IsOn) return;
        string? path = Draft().SoundPath;
        if (path is not null && WarningSound.Problem(path) is string problem) { Hint.Text = problem; return; }
        try
        {
            bool custom = play(path);
            Hint.Text = path is not null && !custom
                ? "Windows could not play this WAV. Export it as PCM WAV. Preview used the Windows sound."
                : "Preview requested. Playback follows Windows volume and mute settings.";
        }
        catch { Hint.Text = "Audio could not be played. Check Windows volume and audio settings."; }
    }
    internal static OpenFileDialog CreateSoundPicker(string selected)
    {
        string? folder = null;
        try { if (Path.IsPathFullyQualified(selected) && Directory.Exists(Path.GetDirectoryName(selected))) folder = Path.GetDirectoryName(selected); }
        catch (ArgumentException) { }
        string fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Media");
        return new OpenFileDialog { Title = "Choose warning sound", Filter = "WAV sound files (*.wav)|*.wav",
            CheckFileExists = true, RestoreDirectory = true,
            InitialDirectory = folder ?? (Directory.Exists(fallback) ? fallback : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)),
            FileName = File.Exists(selected) ? selected : "" };
    }
}
