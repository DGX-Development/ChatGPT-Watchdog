namespace ChatGPTWatchdog;

internal static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        try
        {
            if (args.Length > 0)
            {
                if (args.Length == 3 && args[0] == "--shutdown-helper" && args[1] is "accept" or "ignore" or "delayed")
                { ShutdownDiagnostics.Helper(args[1], Path.GetFullPath(args[2])); return 0; }
                if (args.Length == 2 && args[0] is "--shutdown-self-test" or "--shutdown-locked-test")
                    return ShutdownDiagnostics.Run(Path.GetFullPath(args[1]), args[0] == "--shutdown-locked-test");
                // Visual test modes never create the target/controller or load real settings.
                if (args.Length == 1 && args[0] == "--icon-test") { BrandPreview.ShowIconTest(); return 0; }
                if (args.Length == 2 && args[0] == "--preview") return Preview.Run(Path.GetFullPath(args[1]));
                if (args.Length == 2 && args[0] == "--inspect")
                {
                    var snapshot = WindowsTarget.Read();
                    var input = WindowsTarget.Input();
                    string output = Path.GetFullPath(args[1]);
                    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                    File.WriteAllText(output, System.Text.Json.JsonSerializer.Serialize(new
                    {
                        snapshot.Known, Found = snapshot.Key is not null,
                        Windows = snapshot.Windows.Count, Processes = snapshot.ProcessIds.Count,
                        snapshot.Error, MainProcesses = snapshot.Windows.Select(w => w.ProcessId).Distinct().Count(), InputAvailable = input.HasValue, Locked = WindowsTarget.LockState()
                    }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                    return snapshot.Known && input.HasValue ? 0 : 1;
                }
                throw new ArgumentException("Usage: ChatGPT-Watchdog.exe [--preview <output directory>]");
            }
            using var mutex = new Mutex(false, $"Local\\DGX.ChatGPTWatchdog.{System.Diagnostics.Process.GetCurrentProcess().SessionId}");
            bool owns;
            try { owns = mutex.WaitOne(0); } catch (AbandonedMutexException) { owns = true; }
            if (!owns)
            {
                if (!SingleInstanceChannel.TryShowSettings())
                    MessageBox.Show("Watchdog is already running. Open Settings from its notification-area icon.", "ChatGPT Watchdog", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 0;
            }
            try
            {
                var store = new SettingsStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChatGPT Watchdog"));
                using var application = new TrayApplication(store, store.Load());
                Application.Run(application);
            }
            finally { mutex.ReleaseMutex(); }
            return 0;
        }
        catch (Exception error)
        {
            if (args.Contains("--preview") || args.Contains("--inspect"))
            {
                if (args.Length == 2 && args[0] == "--preview")
                    File.WriteAllText(Path.Combine(Path.GetFullPath(args[1]), "preview-error.txt"), error.ToString());
                return 1;
            }
            MessageBox.Show(error.Message, "ChatGPT Watchdog could not start", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return 1;
        }
    }
}

internal static class Preview
{
    public static int Run(string directory)
    {
        Directory.CreateDirectory(directory);
        foreach (string theme in new[] { "Light", "Dark" })
        {
            var value = new Settings(Appearance: theme);
            using var settings = new SettingsForm(value, draft => draft);
            Render(settings, Path.Combine(directory, $"settings-{theme.ToLowerInvariant()}.png"), false);
            settings.AppearanceField.Open(); Application.DoEvents();
            RenderPopup(settings.AppearanceField.Popup, Path.Combine(directory, $"appearance-{theme.ToLowerInvariant()}.png"));
            settings.AppearanceField.Popup.Close();
            var file = (ToolStripMenuItem)settings.NavigationMenu.Items[0]; file.ShowDropDown(); Application.DoEvents();
            RenderPopup(file.DropDown, Path.Combine(directory, $"file-menu-{theme.ToLowerInvariant()}.png")); file.HideDropDown();
            var preferences = (ToolStripMenuItem)settings.NavigationMenu.Items[1]; preferences.ShowDropDown(); Application.DoEvents();
            RenderPopup(preferences.DropDown, Path.Combine(directory, $"settings-menu-{theme.ToLowerInvariant()}.png"));
            settings.AppearanceMenu.ShowDropDown(); Application.DoEvents();
            RenderPopup(settings.AppearanceMenu.DropDown, Path.Combine(directory, $"appearance-menu-{theme.ToLowerInvariant()}.png"));
            settings.AppearanceMenu.HideDropDown(); preferences.HideDropDown();
            settings.SetMonitoringPaused(true); preferences.ShowDropDown(); Application.DoEvents();
            RenderPopup(preferences.DropDown, Path.Combine(directory, $"settings-menu-paused-{theme.ToLowerInvariant()}.png")); preferences.HideDropDown();
            VerifyTrayMenu(directory, theme);
            var helpMenu = (ToolStripMenuItem)settings.NavigationMenu.Items[2]; helpMenu.ShowDropDown(); Application.DoEvents();
            RenderPopup(helpMenu.DropDown, Path.Combine(directory, $"help-menu-{theme.ToLowerInvariant()}.png")); helpMenu.HideDropDown();
            settings.Close();
            using var help = new HelpForm(theme);
            Render(help, Path.Combine(directory, $"help-{theme.ToLowerInvariant()}.png"), false);
            for (int i = 0; i < HelpForm.Topics.Length; i++)
            {
                help.TopicList.SelectedIndex = i; Application.DoEvents();
                foreach (var label in Descendants(help).OfType<Label>().Where(l => l.Dock == DockStyle.Fill))
                {
                    var required = TextRenderer.MeasureText(label.Text, label.Font, new Size(label.ClientSize.Width, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
                    if (required.Height > label.ClientSize.Height) throw new InvalidDataException("Help text is clipped: " + HelpForm.Topics[i].Title);
                }
            }
            help.Close();
            using var notificationOptions = new NotificationOptionsForm(new(), theme, _ => true);
            Render(notificationOptions, Path.Combine(directory, $"notifications-{theme.ToLowerInvariant()}.png"), false);
            notificationOptions.SoundChoice.Popup.Items[1].PerformClick();
            Render(notificationOptions, Path.Combine(directory, $"notifications-custom-{theme.ToLowerInvariant()}.png"));
            using var about = new AboutForm(theme);
            if (AboutForm.DeveloperWebsite != "https://dgx.digitalguy.me" || Descendants(about).OfType<LinkLabel>().Single().Text != "DGX.digitalguy.me")
                throw new InvalidDataException("About must retain the approved Developer Website label and destination.");
            Render(about, Path.Combine(directory, $"about-{theme.ToLowerInvariant()}.png"));
            using var unsaved = new UnsavedForm(theme);
            Render(unsaved, Path.Combine(directory, $"unsaved-{theme.ToLowerInvariant()}.png"));
            using var warning = new WarningForm(value);
            warning.UpdateRemaining(120);
            Render(warning, Path.Combine(directory, $"warning-{theme.ToLowerInvariant()}.png"));
        }
        BrandPreview.RenderSizes(directory);
        VerifyInteractions();
        VerifyNotifications();
        File.WriteAllText(Path.Combine(directory, "verification.txt"), "Light/dark settings, countdown, flat popup/menu, Help, About and unsaved-edit layouts passed. Warning cancel/Escape/close/postpone, number validation, menu save/exit and unsaved-edit actions passed. Tray order, paused-only status heading, removal of menu Postpone commands, both Pause/Resume commands, external pause synchronization, and paused icon sizes passed. Notification options, silent previews, sound-disable behavior, foreground fallback, independent TopMost and locked-state suppression passed. No controller or target close API was instantiated.");
        return 0;
    }
    static void VerifyTrayMenu(string directory, string theme)
    {
        bool paused = false; int settings = 0, help = 0, about = 0, exit = 0;
        using var menu = new TrayMenu(() => settings++, () => paused = !paused, value => { if (value) about++; else help++; }, () => exit++);
        var colors = UiColors.From(theme); MenuStyling.Apply(menu, colors);
        if (menu.Items.OfType<ToolStripMenuItem>().Any(item => item.Text?.StartsWith("Postpone") == true))
            throw new InvalidDataException("Postpone must not appear in the tray menu.");
        if (menu.Items.Count != 9 || menu.Items[2] != menu.Pause || menu.Items[3].Text != "Settings…" || !menu.StatusHeading.Available ||
            menu.Items.OfType<ToolStripMenuItem>().Any(item => item != menu.StatusHeading && !item.Enabled))
            throw new InvalidDataException("Tray menu must contain a status heading, Pause/Resume and Settings, without a folder command.");
        foreach (var (state, expected) in new[] {
            (WatchState.Monitoring, "Monitoring active"),
            (WatchState.Warning, "Force-close countdown"),
            (WatchState.Paused, "Monitoring paused"),
            (WatchState.Unavailable, "Monitoring unavailable"),
            (WatchState.Failed, "Shutdown incomplete"),
            (WatchState.Postponed, "Postponed for 60 min") })
        {
            menu.Update(state, state == WatchState.Paused, true, 60, colors);
            if (!menu.StatusHeading.Available || menu.StatusHeading.CanSelect || menu.StatusHeading.Enabled)
                throw new InvalidDataException("Monitoring status must always be visible and non-clickable.");
            if (menu.StatusText != expected)
                throw new InvalidDataException("Tray status is incorrect: " + state);
            if (menu.Pause.Checked || menu.Pause.CheckOnClick || menu.Pause.Text != (state == WatchState.Paused ? "Resume monitoring" : "Pause monitoring"))
                throw new InvalidDataException("Pause/Resume must use a command label, not a checked box.");
            menu.Show(new Point(30, 30)); Application.DoEvents();
            RenderPopup(menu, Path.Combine(directory, $"tray-{state.ToString().ToLowerInvariant()}-{theme.ToLowerInvariant()}.png")); menu.Close();
        }
        menu.Update(WatchState.Monitoring, false, false, 0, colors);
        if (menu.StatusText != "Waiting for ChatGPT") throw new InvalidDataException("Absent target status is incorrect.");
        menu.Pause.PerformClick(); menu.Update(WatchState.Paused, paused, true, 0, colors);
        if (!paused || menu.Pause.Text != "Resume monitoring") throw new InvalidDataException("Tray pause did not synchronize.");
        menu.Pause.PerformClick();
        if (paused) throw new InvalidDataException("Tray resume failed.");
        foreach (var item in menu.Items.OfType<ToolStripMenuItem>().Where(item => item != menu.Pause && item != menu.StatusHeading)) item.PerformClick();
        if (settings != 1 || help != 1 || about != 1 || exit != 1) throw new InvalidDataException("Tray navigation callbacks changed.");
    }
    static void VerifyNotifications()
    {
        int previews = 0;
        using (var options = new NotificationOptionsForm(new(), "Dark", _ => { previews++; return false; }))
        {
            options.Show(); Application.DoEvents();
            var firstToggle = options.RectangleToClient(options.Foreground.RectangleToScreen(options.Foreground.ClientRectangle));
            var secondToggle = options.RectangleToClient(options.OnTop.RectangleToScreen(options.OnTop.ClientRectangle));
            var browse = Descendants(options).OfType<Button>().Single(button => button.Text == "Browse");
            if (secondToggle.Top - firstToggle.Bottom < 12 || browse.Left - options.SoundPath.Parent!.Right < 12)
                throw new InvalidDataException("Notification controls need distinct horizontal and vertical gaps.");
            options.PreviewSound.PerformClick();
            if (previews != 1 || options.Draft() != new NotificationOptions()) throw new InvalidDataException("Sound preview must not edit options.");
            options.Sound.PerformClick(); options.PreviewSound.PerformClick();
            if (options.PreviewSound.Enabled || previews != 1) throw new InvalidDataException("Sound disabled must prevent preview.");
            options.Foreground.PerformClick(); options.OnTop.PerformClick();
            Descendants(options).OfType<Button>().Single(b => b.Text == "Save options").PerformClick();
            if (options.SelectedOptions != new NotificationOptions(false, null, false, false)) throw new InvalidDataException("Notification toggles did not apply.");
        }
        string selected = Path.Combine(Path.GetTempPath(), "Watchdog-preview-" + Guid.NewGuid().ToString("N") + ".wav");
        try
        {
            using (var file = File.Create(selected))
            using (var writer = new BinaryWriter(file))
            {
                writer.Write("RIFF"u8); writer.Write(196); writer.Write("WAVEfmt "u8); writer.Write(16);
                writer.Write((short)1); writer.Write((short)1); writer.Write(8000); writer.Write(16000);
                writer.Write((short)2); writer.Write((short)16); writer.Write("data"u8); writer.Write(160); writer.Write(new byte[160]);
            }
            string? played = null;
            using var custom = new NotificationOptionsForm(new(), "Light", path => { played = path; return true; });
            custom.Show(); Application.DoEvents(); custom.SoundChoice.Popup.Items[1].PerformClick();
            custom.SoundPath.Text = selected; custom.PreviewSound.PerformClick();
            if (played != selected || custom.SelectedOptions != new NotificationOptions()) throw new InvalidDataException("Custom Preview must use the unsaved selection.");
            played = null; custom.SoundPath.Text = selected + ".missing.wav"; custom.PreviewSound.PerformClick();
            if (played is not null || !custom.Hint.Text.Contains("not found")) throw new InvalidDataException("Invalid preview must explain the error without playback.");
            using var picker = NotificationOptionsForm.CreateSoundPicker(selected);
            if (picker.InitialDirectory != Path.GetDirectoryName(selected) || picker.FileName != selected || !picker.RestoreDirectory)
                throw new InvalidDataException("Sound Browse must start at the selected sound.");
            custom.Close();
        }
        finally { File.Delete(selected); }
        foreach (bool onTop in new[] { false, true })
        {
            using var form = new WarningForm(new Settings { Notifications = new(AlwaysOnTop: onTop) });
            var attention = new PreviewAttention(); form.Show(); Application.DoEvents();
            if (form.TopMost != onTop || attention.Sounds != 0) throw new InvalidDataException("Warning visibility must not itself play sound.");
            form.RequestAttention(false, attention); form.RequestAttention(false, attention);
            if (attention.Sounds != 1 || attention.Foreground != 1 || attention.Flashes != 1) throw new InvalidDataException("Warning requests attention once, with flash fallback.");
            form.Dismiss();
        }
        using (var form = new WarningForm(new()))
        {
            var attention = new PreviewAttention(); form.Show(); Application.DoEvents(); form.RequestAttention(true, attention);
            if (attention.Sounds + attention.Foreground + attention.Flashes != 0) throw new InvalidDataException("Locked state must suppress desktop attention.");
            form.Dismiss();
        }
        Settings? saved = null;
        using var owner = new SettingsForm(new(), value => saved = value);
        owner.Show(); Application.DoEvents(); owner.Idle.Value = 49;
        using var timer = new System.Windows.Forms.Timer { Interval = 50 };
        bool observed = false;
        timer.Tick += (_, _) =>
        {
            if (owner.OwnedForms.FirstOrDefault() is not NotificationOptionsForm dialog) return;
            observed = dialog.Owner == owner && !dialog.ShowInTaskbar; timer.Stop();
            dialog.Sound.PerformClick(); dialog.Foreground.PerformClick();
            Descendants(dialog).OfType<Button>().Single(b => b.Text == "Save options").PerformClick();
        };
        timer.Start();
        ((ToolStripMenuItem)owner.NavigationMenu.Items[1]).DropDownItems.OfType<ToolStripMenuItem>().Single(item => item.Text == "&Notifications…").PerformClick();
        if (!observed || !owner.Dirty || saved?.IdleMinutes != 45 || saved.Notifications.SoundEnabled || saved.Notifications.BringToForeground || owner.Idle.Value != 49)
            throw new InvalidDataException("Save options must persist notifications while preserving other unsaved fields.");
        owner.Save();
        if (saved?.Notifications.SoundEnabled != false || saved.Notifications.BringToForeground || saved.IdleMinutes != 49 || owner.Dirty)
            throw new InvalidDataException("Save settings did not persist notification options.");
        owner.Close();
    }
    sealed class PreviewAttention : IWarningAttention
    {
        internal int Sounds, Foreground, Flashes;
        public bool PlaySound(string? path) { Sounds++; return true; }
        public bool TryForeground() { Foreground++; return false; }
        public void FlashTaskbar() => Flashes++;
    }
    static IEnumerable<Control> Descendants(Control control) => control.Controls.Cast<Control>()
        .SelectMany(child => new[] { child }.Concat(Descendants(child)));
    static void VerifyInteractions()
    {
        foreach (string action in new[] { "Cancel", "Postpone 60 min", "Close" })
        {
            using var form = new WarningForm(new());
            int canceled = 0, postponed = 0;
            form.Canceled += () => { canceled++; form.Dismiss(); };
            form.Postponed += () => { postponed++; form.Dismiss(); };
            form.Show(); Application.DoEvents();
            if (form.CancelButton is not Button escape || escape.Text != "Cancel")
                throw new InvalidDataException("Escape must cancel the warning.");
            if (action == "Close") form.Close();
            else Descendants(form).OfType<Button>().Single(button => button.Text == action).PerformClick();
            Application.DoEvents();
            if (canceled != (action == "Postpone 60 min" ? 0 : 1) || postponed != (action == "Postpone 60 min" ? 1 : 0) || !form.IsDisposed)
                throw new InvalidDataException("Warning action failed: " + action);
        }
        Settings? saved = null;
        using var settings = new SettingsForm(new(), value => saved = value);
        settings.Show(); Application.DoEvents();
        settings.Idle.Value = 50;
        Descendants(settings).OfType<Button>().Single(button => button.Text == "Save settings").PerformClick();
        if (saved?.IdleMinutes != 50 || settings.IsDisposed || settings.Dirty)
            throw new InvalidDataException("Settings draft/save interaction failed.");
        if (Descendants(settings).OfType<Button>().Single(b => b.Text == "Saved").Enabled)
            throw new InvalidDataException("Saved button must be disabled.");
        settings.Idle.Value = 51; settings.UnsavedDecision = () => DialogResult.Cancel;
        settings.Close(); if (settings.IsDisposed) throw new InvalidDataException("Keep editing must preserve the draft.");
        settings.UnsavedDecision = () => DialogResult.Yes;
        settings.Close(); if (!settings.IsDisposed || saved?.IdleMinutes != 51) throw new InvalidDataException("Save-on-close failed.");
        using var discard = new SettingsForm(new(), _ => throw new Exception("Discard must not save"));
        discard.Show(); Application.DoEvents(); discard.Idle.Value = 46;
        discard.UnsavedDecision = () => DialogResult.No; discard.RequestExit();
        if (!discard.IsDisposed || !discard.ExitOnClose) throw new InvalidDataException("Discard/exit did not close settings.");
        using var menuSave = new SettingsForm(new(), value => saved = value);
        menuSave.Show(); Application.DoEvents(); menuSave.Idle.Value = 47;
        ((ToolStripMenuItem)menuSave.NavigationMenu.Items[0]).DropDownItems[0].PerformClick();
        if (saved?.IdleMinutes != 47 || menuSave.Dirty) throw new InvalidDataException("File > Save settings failed.");
        bool pause = false;
        menuSave.PauseRequested += value => pause = value;
        var settingsMenu = (ToolStripMenuItem)menuSave.NavigationMenu.Items[1];
        settingsMenu.DropDownItems[0].PerformClick();
        if (!pause) throw new InvalidDataException("Pause menu action did not raise its request.");
        if (settingsMenu.DropDownItems.OfType<ToolStripMenuItem>().Any(item => item.Text?.StartsWith("Postpone") == true) || menuSave.NavigationMenu.Items.Count != 3)
            throw new InvalidDataException("Postpone belongs only on countdown; Notifications belongs under Settings.");
        var pauseCommand = (ToolStripMenuItem)settingsMenu.DropDownItems[0];
        if (pauseCommand.Text != "&Resume monitoring" || pauseCommand.Checked || pauseCommand.CheckOnClick)
            throw new InvalidDataException("Settings pause must change to an unchecked Resume command.");
        pauseCommand.PerformClick();
        if (pause || pauseCommand.Text != "&Pause monitoring") throw new InvalidDataException("Settings Resume failed.");
        menuSave.IsPaused = () => true;
        settingsMenu.ShowDropDown(); Application.DoEvents();
        if (pauseCommand.Text != "&Resume monitoring") throw new InvalidDataException("Settings menu did not synchronize an external pause.");
        settingsMenu.HideDropDown();
        foreach (bool about in new[] { false, true })
        {
            bool observed = false;
            using var closer = new System.Windows.Forms.Timer { Interval = 50 };
            closer.Tick += (_, _) =>
            {
                if (menuSave.OwnedForms.FirstOrDefault() is not CompactForm child) return;
                observed = child.Owner == menuSave && !child.ShowInTaskbar && child.Theme == menuSave.Theme;
                child.Close(); closer.Stop();
            };
            closer.Start();
            var helpMenu = (ToolStripMenuItem)menuSave.NavigationMenu.Items[2];
            helpMenu.DropDownItems[about ? 3 : 0].PerformClick();
            if (!observed) throw new InvalidDataException("Help/About must be themed, owned dialogs.");
        }
        menuSave.Close();
        using var number = new NumberField(1, 1440, 45);
        number.Increase.PerformClick(); if (number.Value != 46) throw new InvalidDataException("Number increment failed.");
        number.Decrease.PerformClick(); if (number.Value != 45) throw new InvalidDataException("Number decrement failed.");
        number.Input.Text = "invalid";
        try { _ = number.Value; throw new Exception("Invalid numeric text was silently accepted."); } catch (InvalidDataException) { }
    }
    static void RenderPopup(ToolStrip popup, string path)
    {
        if (popup.Renderer is not FlatMenuRenderer) throw new InvalidDataException("Native blue checkboxes must not be used.");
        using var bitmap = new Bitmap(popup.Width, popup.Height);
        popup.DrawToBitmap(bitmap, new Rectangle(Point.Empty, popup.Size)); bitmap.Save(path);
    }
    static void Render(Form form, string path, bool close = true)
    {
        form.Show(); Application.DoEvents();
        void Check(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                if (!control.Visible) continue;
                if (control.Left < 0 || control.Top < 0 || control.Right > parent.ClientSize.Width + 1 || control.Bottom > parent.ClientSize.Height + 1)
                    throw new InvalidDataException($"Control outside visible window: {control.GetType().Name}; name={control.Name}; text={control.Text}; bounds={control.Bounds}; parent={parent.GetType().Name} {parent.ClientSize}; dpi={control.DeviceDpi}");
                if (control is ComboBox picker && picker.SelectedIndex < 0)
                    throw new InvalidDataException("Appearance has no selected value.");
                if (control is Label label && (control.Parent is DangerNotice || form is NotificationOptionsForm) && label.Dock == DockStyle.Fill)
                {
                    var required = TextRenderer.MeasureText(label.Text, label.Font, new Size(label.ClientSize.Width, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
                    if (required.Height > label.ClientSize.Height) throw new InvalidDataException("Force-close warning is clipped: " + label.Text);
                }
                if (control is Panel) Check(control);
            }
        }
        Check(form);
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size)); bitmap.Save(path);
        if (close) form.Close();
    }
}









