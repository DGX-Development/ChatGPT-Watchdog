namespace ChatGPTWatchdog;

internal static class BrandPreview
{
    // This interactive visual test has no settings store, timing engine, target discovery or close API.
    internal static void ShowIconTest()
    {
        using var window = new AboutForm("System")
        {
            Text = "ChatGPT Watchdog - Icon test (monitoring off)",
            ShowInTaskbar = true,
            StartPosition = FormStartPosition.CenterScreen
        };
        using var trayIcon = AppBrand.LoadIcon(SystemInformation.SmallIconSize.Width);
        using var menu = new ContextMenuStrip();
        menu.Items.Add("Show icon test", null, (_, _) => { window.Show(); window.Activate(); });
        menu.Items.Add("Close icon test", null, (_, _) => window.Close());
        MenuStyling.Apply(menu, UiColors.From("System"));
        using var tray = new NotifyIcon { Icon = trayIcon, Text = "ChatGPT Watchdog - Icon test only; monitoring is OFF", ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += (_, _) => { window.Show(); window.Activate(); };
        Application.Run(window);
        tray.Visible = false;
    }

    static void RenderPauseSizes(string directory)
    {
        using var sheet = new Bitmap(620, 240); using var graphics = Graphics.FromImage(sheet);
        graphics.Clear(Color.FromArgb(243, 243, 243));
        using var font = new Font("Segoe UI Variable Text", 10);
        graphics.DrawString("Normal and paused icons at actual size", font, Brushes.Black, 15, 12);
        graphics.FillRectangle(Brushes.White, 10, 62, 600, 60);
        using var dark = new SolidBrush(Color.FromArgb(32, 32, 32)); graphics.FillRectangle(dark, 10, 132, 600, 60);
        int[] sizes = [16, 20, 24, 32, 40, 48];
        for (int i = 0; i < sizes.Length; i++)
        {
            int size = sizes[i], left = 30 + i * 98;
            using var normal = AppBrand.LoadIcon(size); using var paused = AppBrand.LoadPausedIcon(size);
            using var bitmap = paused.ToBitmap(); using var original = normal.ToBitmap();
            if (paused.Width != size || paused.Height != size || bitmap.GetPixel(0, 0).A != 0)
                throw new InvalidDataException("Paused icon frame or transparency is incorrect.");
            bool different = false;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                if (bitmap.GetPixel(x, y) != original.GetPixel(x, y)) different = true;
            if (!different) throw new InvalidDataException("Pause badge is missing.");
            bitmap.Save(Path.Combine(directory, $"icon-paused-{size}.png"));
            graphics.DrawString($"{size} px", font, Brushes.Black, left, 38);
            foreach (int top in new[] { 68, 138 })
            {
                graphics.DrawIconUnstretched(normal, new Rectangle(left - 12, top + (48 - size) / 2, size, size));
                graphics.DrawIconUnstretched(paused, new Rectangle(left + 38, top + (48 - size) / 2, size, size));
            }
        }
        graphics.DrawString("Amber dot only. Approved base artwork remains unchanged.", font, Brushes.Black, 15, 208);
        sheet.Save(Path.Combine(directory, "icon-pause-comparison.png"));
    }
    internal static void RenderSizes(string directory)
    {
        int[] sizes = [16, 20, 24, 32, 40, 48, 64, 256];
        foreach (int size in sizes)
        {
            using var icon = AppBrand.LoadIcon(size);
            if (icon.Width != size || icon.Height != size) throw new InvalidDataException($"Incorrect icon frame selected for {size}px.");
            using var bitmap = icon.ToBitmap();
            if (bitmap.GetPixel(0, 0).A != 0) throw new InvalidDataException($"Icon {size}px has an opaque background.");
            bitmap.Save(Path.Combine(directory, $"icon-{size}.png"));
        }
        RenderPauseSizes(directory);
        using var sheet = new Bitmap(1000, 440);
        using var graphics = Graphics.FromImage(sheet);
        using var heading = new Font("Segoe UI Variable Text", 16, FontStyle.Bold);
        using var label = new Font("Segoe UI Variable Text", 10);
        using var dark = new SolidBrush(Color.FromArgb(32, 32, 32));
        graphics.Clear(Color.FromArgb(243, 243, 243));
        graphics.DrawString("Approved watchdog - actual Windows icon frames", heading, Brushes.Black, 25, 20);
        graphics.DrawString("256 px", label, Brushes.Black, 130, 70);
        using (var large = AppBrand.LoadIcon(256)) graphics.DrawIconUnstretched(large, new Rectangle(30, 100, 256, 256));
        graphics.FillRectangle(Brushes.White, 320, 115, 660, 120);
        graphics.FillRectangle(dark, 320, 250, 660, 120);
        for (int i = 0; i < 7; i++)
        {
            int size = sizes[i], center = 365 + i * 90;
            graphics.DrawString($"{size} px", label, Brushes.Black, center - 18, 85);
            using var icon = AppBrand.LoadIcon(size);
            graphics.DrawIconUnstretched(icon, new Rectangle(center - size / 2, 175 - size / 2, size, size));
            graphics.DrawIconUnstretched(icon, new Rectangle(center - size / 2, 310 - size / 2, size, size));
        }
        graphics.DrawString("View at 100% zoom. Test only - no monitoring or ChatGPT closure.", label, Brushes.Black, 25, 400);
        sheet.Save(Path.Combine(directory, "icon-size-test.png"));
    }
}
