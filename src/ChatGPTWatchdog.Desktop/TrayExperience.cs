using W = System.Windows;
using M = System.Windows.Media;
using F = System.Windows.Forms;
using System.Windows.Media.Imaging;

namespace ChatGPTWatchdog;
internal sealed partial class ReviewWindow
{
    System.Drawing.Icon? trayRunning, trayPaused;
    F.ToolStripMenuItem? trayStatus;
    F.ToolStripMenuItem? trayMonitoring;
    internal F.ContextMenuStrip? ReviewTray => tray?.ContextMenuStrip;

    internal void EnsureTray()
    {
        if (tray != null) { UpdateTray(); tray.Visible = true; return; }
        var menu = new DashboardTrayMenu { Renderer = new DashboardMenuRenderer(TrayColor), ShowImageMargin = true, Font=new System.Drawing.Font("Segoe UI",10.5f), Padding=new F.Padding(3) };
        trayStatus = new F.ToolStripMenuItem { Enabled = false, Tag = "status-paused" };
        menu.Items.Add(trayStatus);
        menu.Items.Add(new F.ToolStripSeparator());
        F.ToolStripMenuItem Add(string label, string glyph, Action action)
        {
            var item = new F.ToolStripMenuItem(label) { Tag = glyph };
            item.Click += (_, _) => Window.Dispatcher.Invoke(() => Guard(action));
            menu.Items.Add(item);
            return item;
        }
        Add("Open ChatGPT Watchdog", "dashboard", Reopen);
        trayMonitoring = Add("Resume monitoring", "eye_off-16", () =>
        {
            if (ReviewEntry.Safe) return;
            if (runtime == null || runtime.Paused) StartMonitoring();
            else runtime.Pause();
            UpdateTray();
        });
        trayMonitoring.Enabled = !ReviewEntry.Safe;
        menu.Items.Add(new F.ToolStripSeparator());
        Add("Exit", "exit", Exit);
        menu.Opening += (_, _) =>
        {
            UpdateTray(); menu.BackColor = TrayColor("2B2B2B"); menu.ForeColor = TrayColor("F2F2F2");
            foreach (var item in menu.Items.OfType<F.ToolStripMenuItem>())
            {
                item.ForeColor = menu.ForeColor; item.Padding = new F.Padding(12, 3, 12, 3);
                item.Image?.Dispose(); item.Image = TrayImage((string)item.Tag!);
            }
        };
        trayRunning = AppBrand.LoadIcon(F.SystemInformation.SmallIconSize.Width);
        trayPaused = AppBrand.LoadPausedIcon(F.SystemInformation.SmallIconSize.Width);
        tray = new F.NotifyIcon { ContextMenuStrip = menu, Icon = trayPaused, Visible = true };
        tray.DoubleClick += (_, _) => Window.Dispatcher.Invoke(Reopen);
        UpdateTray();
    }

    void OpenLogs(){var path=Path.Combine(directory,"logs");Directory.CreateDirectory(path);Open(path);}
    void UpdateTray()
    {
        if (tray == null) return;
        bool paused = ReviewEntry.Safe || runtime == null || runtime.Paused;
        if(trayMonitoring != null){trayMonitoring.Text=paused?"Resume monitoring":"Pause monitoring";trayMonitoring.Tag=paused?"eye_off-16":"eye-16";}
        tray.Icon = paused ? trayPaused : trayRunning;
        tray.Text = "ChatGPT Watchdog: " + (ReviewEntry.Safe ? "Safe review, monitoring off" : paused ? "Monitoring paused" : "Monitoring active");
        if(trayStatus != null){trayStatus.Text=paused?"Monitoring paused":"Monitoring active";trayStatus.Tag=paused?"status-paused":"status-active";}
    }
    System.Drawing.Color TrayColor(string key)
    {
        var c=((M.SolidColorBrush)W.Application.Current.Resources["Theme"+key]).Color;
        return System.Drawing.Color.FromArgb(c.A,c.R,c.G,c.B);
    }
    System.Drawing.Bitmap TrayImage(string name)
    {
        if(name.StartsWith("status-")){var dot=new System.Drawing.Bitmap(16,16);using var graphics=System.Drawing.Graphics.FromImage(dot);graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;using var brush=new System.Drawing.SolidBrush(name=="status-active"?System.Drawing.Color.FromArgb(64,180,104):TrayColor("969696"));graphics.FillEllipse(brush,4,4,8,8);return dot;}
        var image=Glyph(name,16); image.Measure(new W.Size(16,16));image.Arrange(new W.Rect(0,0,16,16));
        var bitmap=new RenderTargetBitmap(16,16,96,96,M.PixelFormats.Pbgra32);bitmap.Render(image);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream=new MemoryStream();encoder.Save(stream);stream.Position=0;
        using var result=new System.Drawing.Bitmap(stream);return new System.Drawing.Bitmap(result);
    }
    void DisposeTray()
    {
        var menu=tray?.ContextMenuStrip; if(menu!=null)foreach(F.ToolStripItem item in menu.Items)item.Image?.Dispose();
        tray?.Dispose();menu?.Dispose();trayRunning?.Dispose();trayPaused?.Dispose();
    }
}
