using System.Drawing.Drawing2D;
namespace ChatGPTWatchdog;

// Deliberately distinct from neutral cards, while retaining the same rounded geometry.
internal sealed class DangerNotice : Panel
{
    readonly Font headingFont = new("Segoe UI Variable Text", 10.5f, FontStyle.Bold);
    readonly Label heading = new() { Dock = DockStyle.Top, Height = 27, Margin = Padding.Empty };
    readonly Label body = new() { Dock = DockStyle.Fill, Margin = Padding.Empty };
    Color border, accent;
    internal DangerNotice(string title, string message)
    {
        DoubleBuffered = true; ResizeRedraw = true; Padding = new Padding(48, 16, 18, 14);
        heading.Text = title; heading.Font = headingFont; body.Text = message;
        Controls.Add(body); Controls.Add(heading);
        AccessibleName = title; AccessibleDescription = message; TabStop = false;
    }
    internal void SetColors(bool dark)
    {
        BackColor = dark ? Color.FromArgb(57, 33, 37) : Color.FromArgb(255, 241, 243);
        border = dark ? Color.FromArgb(166, 76, 90) : Color.FromArgb(213, 139, 151);
        accent = dark ? Color.FromArgb(255, 165, 179) : Color.FromArgb(157, 29, 50);
        heading.BackColor = body.BackColor = BackColor; heading.ForeColor = accent;
        body.ForeColor = dark ? Color.FromArgb(255, 222, 228) : Color.FromArgb(111, 36, 50);
        Invalidate(true);
    }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? BackColor);
        if (Width < 2 || Height < 2) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var shape = Drawing.Rounded(new RectangleF(.5f, .5f, Width - 1, Height - 1), 8 * DeviceDpi / 96f);
        using var fill = new SolidBrush(BackColor); using var pen = new Pen(border);
        e.Graphics.FillPath(fill, shape); e.Graphics.DrawPath(pen, shape);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); float s = DeviceDpi / 96f;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(accent, 1.7f * s) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
        e.Graphics.DrawPolygon(pen, [new PointF(25*s,20*s),new(35*s,38*s),new(15*s,38*s)]);
        e.Graphics.DrawLine(pen,25*s,26*s,25*s,31*s);
        using var brush = new SolidBrush(accent); e.Graphics.FillEllipse(brush,24*s,33*s,2*s,2*s);
    }
    protected override void Dispose(bool disposing) { base.Dispose(disposing); if (disposing) headingFont.Dispose(); }
}
