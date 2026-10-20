using System.ComponentModel;
using System.Drawing.Drawing2D;
using Microsoft.Win32;

namespace ChatGPTWatchdog;

internal sealed record UiColors(Color Window, Color Surface, Color Text, Color Muted, Color Accent,
    Color Button, Color Border, Color FieldBorder, Color Hover, bool Dark)
{
    internal static UiColors From(string choice)
    {
        bool dark = choice == "Dark";
        if (choice == "System")
        {
            try { dark = (int?)Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) == 0; }
            catch { dark = false; }
        }
        if (SystemInformation.HighContrast)
            return new(SystemColors.Window, SystemColors.Window, SystemColors.WindowText, SystemColors.GrayText,
                SystemColors.Highlight, SystemColors.Control, SystemColors.WindowText, SystemColors.WindowText, SystemColors.Highlight, false);
        Color C(string light, string night) => ColorTranslator.FromHtml(dark ? night : light);
        return new(C("#F3F3F3", "#202020"), C("#FFFFFF", "#2B2B2B"), C("#1C2534", "#F2F4F8"),
            C("#596779", "#B0B8C6"), C("#2468BB", "#78B7FF"), C("#FAFAFA", "#353535"),
            C("#E5E5E5", "#3B3B3B"), C("#DCDCDC", "#4A4A4A"), C("#E7EEF7", "#353535"), dark);
    }
}

internal static class Drawing
{
    internal static GraphicsPath Rounded(RectangleF bounds, float radius)
    {
        float diameter = radius * 2;
        var path = new GraphicsPath();
        foreach (var (x, y, start) in new[] { (bounds.Left, bounds.Top, 180f),
            (bounds.Right - diameter, bounds.Top, 270f), (bounds.Right - diameter, bounds.Bottom - diameter, 0f),
            (bounds.Left, bounds.Bottom - diameter, 90f) }) path.AddArc(x, y, diameter, diameter, start, 90);
        path.CloseFigure(); return path;
    }
}

internal sealed class SurfacePanel : Panel
{
    internal Color Border = Color.LightGray;
    internal int Radius = 8;
    internal SurfacePanel() { DoubleBuffered = true; ResizeRedraw = true; }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? BackColor);
        if (Width < 2 || Height < 2) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var shape = Drawing.Rounded(new RectangleF(.5f, .5f, Width - 1, Height - 1), Radius * DeviceDpi / 96f);
        using var brush = new SolidBrush(BackColor); using var pen = new Pen(Border);
        e.Graphics.FillPath(brush, shape); e.Graphics.DrawPath(pen, shape);
    }
}

internal class ActionButton : Button
{
    internal Color Border = Color.Gray;
    internal Color Muted = Color.Gray;
    bool hovered;
    bool pressed;
    internal ActionButton()
    {
        FlatStyle = FlatStyle.Flat;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Cursor = Cursors.Hand; UseVisualStyleBackColor = false;
    }
    protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovered = pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? BackColor);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Color fill = Enabled && hovered ? pressed ? ControlPaint.Dark(BackColor, .07f) : ControlPaint.Light(BackColor, .09f) : BackColor;
        using var shape = Drawing.Rounded(new RectangleF(.5f, .5f, Width - 1, Height - 1), 4 * DeviceDpi / 96f);
        using var brush = new SolidBrush(fill); using var pen = new Pen(Border);
        e.Graphics.FillPath(brush, shape); e.Graphics.DrawPath(pen, shape);
        var bounds = Rectangle.FromLTRB(Padding.Left, Padding.Top, Width - Padding.Right, Height - Padding.Bottom);
        var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
            (TextAlign == ContentAlignment.MiddleLeft ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter);
        TextRenderer.DrawText(e.Graphics, Text, Font, bounds, Enabled ? ForeColor : Muted, flags);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -4, -4));
    }
}

internal sealed class NumberField : Panel
{
    internal readonly TextBox Input = new() { BorderStyle = BorderStyle.None };
    internal readonly ActionButton Decrease = new() { Text = "-", TabStop = false, AccessibleName = "Decrease value" };
    internal readonly ActionButton Increase = new() { Text = "+", TabStop = false, AccessibleName = "Increase value" };
    readonly int minimum, maximum;
    internal event Action? Changed;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal int Value
    {
        get => int.TryParse(Input.Text, out int value) && value >= minimum && value <= maximum
            ? value : throw new InvalidDataException($"{AccessibleName}: enter a number from {minimum} to {maximum}.");
        set { Input.Text = Math.Clamp(value, minimum, maximum).ToString(); RefreshButtons(); }
    }
    internal NumberField(int minimum, int maximum, int value)
    {
        this.minimum = minimum; this.maximum = maximum;
        Controls.AddRange([Input, Decrease, Increase]); Value = value;
        Input.TextChanged += (_, _) => { RefreshButtons(); Changed?.Invoke(); };
        Decrease.Click += (_, _) => Adjust(-1); Increase.Click += (_, _) => Adjust(1);
        Input.KeyDown += (_, e) =>
        {
            if (e.KeyCode is not (Keys.Up or Keys.Down)) return;
            Adjust(e.KeyCode == Keys.Up ? 1 : -1); e.SuppressKeyPress = true;
        };
    }
    void Adjust(int amount)
    { if (int.TryParse(Input.Text, out int value)) Value = Math.Clamp(value + amount, minimum, maximum); }
    void RefreshButtons()
    {
        bool valid = int.TryParse(Input.Text, out int value);
        Decrease.Enabled = valid && value > minimum; Increase.Enabled = valid && value < maximum;
    }
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int side = 30 * DeviceDpi / 96, gap = 6 * DeviceDpi / 96;
        Increase.SetBounds(Width - side, 0, side, Height);
        Decrease.SetBounds(Increase.Left - gap - side, 0, side, Height);
        Input.SetBounds(0, Math.Max(0, (Height - Input.PreferredHeight) / 2), Math.Max(20, Decrease.Left - gap), Input.PreferredHeight);
        Input.AccessibleName = AccessibleName;
    }
}

// Use the same flat menu treatment for the tray, menu bar, and field picker.
internal sealed class FlatMenuRenderer(UiColors colors) : ToolStripProfessionalRenderer(new MenuPalette(colors))
{
    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        bool status = e.Item.Tag is string tag && tag.StartsWith("status-", StringComparison.Ordinal);
        using var fill = new SolidBrush(!status && (e.Item.Selected || e.Item.Pressed) ? colors.Hover : e.ToolStrip?.BackColor ?? colors.Surface);
        e.Graphics.FillRectangle(fill, new Rectangle(Point.Empty, e.Item.Size));
    }
    protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
    {
        if (e.Item.Tag is string tag && tag.StartsWith("status-", StringComparison.Ordinal) && e.Image is not null)
            e.Graphics.DrawImage(e.Image, e.ImageRectangle);
        else base.OnRenderItemImage(e);
    }
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        if (e.Item.Tag is string tag && tag.StartsWith("status-", StringComparison.Ordinal))
            TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, e.TextRectangle, e.Item.ForeColor, e.TextFormat);
        else base.OnRenderItemText(e);
    }
    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var box = e.ImageRectangle; float unit = Math.Max(1, Math.Min(box.Width, box.Height) / 16f);
        float x = box.Left + 3 * unit, y = box.Top + 8 * unit;
        using var pen = new Pen(colors.Accent, 1.8f * unit) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        e.Graphics.DrawLines(pen, [new PointF(x, y), new(x + 3 * unit, y + 3 * unit), new(x + 10 * unit, y - 4 * unit)]);
    }
    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        var box = e.ArrowRectangle; float x = box.Left + box.Width / 2f, y = box.Top + box.Height / 2f;
        using var pen = new Pen(e.Item?.ForeColor ?? colors.Text, 1.4f);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.DrawLines(pen, e.Direction == ArrowDirection.Down ? [new PointF(x - 3, y - 1), new(x, y + 2), new(x + 3, y - 1)]
            : [new PointF(x - 1, y - 3), new(x + 2, y), new(x - 1, y + 3)]);
    }
    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    { using var pen = new Pen(colors.Border); e.Graphics.DrawLine(pen, 12, e.Item.Height / 2, e.Item.Width - 12, e.Item.Height / 2); }
    sealed class MenuPalette(UiColors p) : ProfessionalColorTable
    {
        public override Color CheckBackground => p.Surface;
        public override Color CheckSelectedBackground => p.Hover;
        public override Color CheckPressedBackground => p.Hover;
        public override Color ToolStripDropDownBackground => p.Surface;
        public override Color ImageMarginGradientBegin => p.Surface;
        public override Color ImageMarginGradientMiddle => p.Surface;
        public override Color ImageMarginGradientEnd => p.Surface;
        public override Color MenuBorder => p.Border;
        public override Color MenuItemBorder => p.Hover;
        public override Color MenuItemSelected => p.Hover;
        public override Color MenuItemSelectedGradientBegin => p.Hover;
        public override Color MenuItemSelectedGradientEnd => p.Hover;
        public override Color MenuItemPressedGradientBegin => p.Hover;
        public override Color MenuItemPressedGradientMiddle => p.Hover;
        public override Color MenuItemPressedGradientEnd => p.Hover;
        public override Color ToolStripGradientBegin => p.Surface;
        public override Color ToolStripGradientMiddle => p.Surface;
        public override Color ToolStripGradientEnd => p.Surface;
    }
}

internal sealed class AppearancePicker : ActionButton
{
    readonly ContextMenuStrip choices = new() { ShowImageMargin = false, ShowCheckMargin = true };
    internal ContextMenuStrip Popup => choices;
    internal event Action? Changed;
    string value = "System";
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal string Value { get => value; set { this.value = value; Text = value; Invalidate(); } }
    internal AppearancePicker(params string[] options)
    {
        TextAlign = ContentAlignment.MiddleLeft; Padding = new Padding(0, 0, 26, 0);
        foreach (string choice in options.Length == 0 ? new[] { "System", "Light", "Dark" } : options)
            choices.Items.Add(choice, null, (_, _) => { Value = choice; Changed?.Invoke(); });
        Click += (_, _) => Open();
    }
    internal void SetColors(UiColors colors)
    {
        MenuStyling.Apply(choices, colors);
        foreach (ToolStripMenuItem item in choices.Items) item.Checked = item.Text == value;
    }
    internal void Open()
    {
        foreach (ToolStripMenuItem item in choices.Items) item.Checked = item.Text == value;
        choices.Font = Font; choices.Show(this, new Point(0, Height));
    }
    protected override void OnKeyDown(KeyEventArgs e)
    { if (e.Alt && e.KeyCode == Keys.Down) { Open(); e.SuppressKeyPress = true; } base.OnKeyDown(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); float s = DeviceDpi / 96f, x = Width - 18 * s, y = Height / 2f - s;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(ForeColor, 1.3f * s);
        e.Graphics.DrawLines(pen, [new PointF(x, y), new(x + 4 * s, y + 4 * s), new(x + 8 * s, y)]);
    }
    protected override void Dispose(bool disposing) { if (disposing) choices.Dispose(); base.Dispose(disposing); }
}

internal static class MenuStyling
{
    internal static void Apply(ToolStrip strip, UiColors colors)
    {
        strip.Renderer = new FlatMenuRenderer(colors); strip.BackColor = colors.Surface; strip.ForeColor = colors.Text;
        if (strip is ToolStripDropDownMenu dropdown)
        {
            bool choices = strip.Items.OfType<ToolStripMenuItem>().All(item => item.Text is "System" or "Light" or "Dark");
            dropdown.ShowImageMargin = !choices; dropdown.ShowCheckMargin = choices;
        }
        foreach (ToolStripItem item in strip.Items)
        {
            item.BackColor = colors.Surface; item.ForeColor = colors.Text;
            if (item.Tag is string tag && tag.StartsWith("status-", StringComparison.Ordinal))
                item.ForeColor = tag == "status-active"
                    ? colors.Dark ? Color.FromArgb(117, 218, 149) : Color.FromArgb(22, 115, 58)
                    : tag == "status-warning"
                        ? colors.Dark ? Color.FromArgb(255, 160, 160) : Color.FromArgb(160, 35, 47)
                        : colors.Dark ? Color.FromArgb(255, 207, 100) : Color.FromArgb(125, 80, 0);
            if (strip is not MenuStrip) MenuSymbols.Apply(item, item.ForeColor);
            if (item is ToolStripMenuItem menu && menu.HasDropDownItems) Apply(menu.DropDown, colors);
        }
    }
}


// Locally drawn line glyphs; no application artwork or other project's assets are embedded.
internal static class MenuSymbols
{
    sealed class OwnedImage { internal Bitmap? Image; }
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ToolStripItem, OwnedImage> images = new();
    internal static void Apply(ToolStripItem item, Color color)
    {
        string label = item.Text?.Replace("&", "").ToLowerInvariant() ?? "";
        string? glyph = item.Tag is string tag && tag.StartsWith("status-", StringComparison.Ordinal)
            ? tag == "status-active" ? "active-dot" : tag == "status-postponed" ? "clock" : "warning"
            : label switch
        {
            var s when s.StartsWith("open data") => "folder",
            var s when s.StartsWith("save") => "save",
            var s when s.StartsWith("close") => "close",
            var s when s.StartsWith("exit") => "exit",
            var s when s.StartsWith("pause") => "pause",
            var s when s.StartsWith("resume") => "resume",
            var s when s.StartsWith("postpone") => "clock",
            var s when s.StartsWith("appearance") => "appearance",
            var s when s.StartsWith("help") => "help",
            var s when s.StartsWith("report an issue") => "report",
            var s when s.StartsWith("about") => "about",
            var s when s.StartsWith("settings") || s.StartsWith("notifications") => "settings",
            _ => null
        };
        if (glyph is null) return;
        var owner = images.GetValue(item, key =>
        {
            var value = new OwnedImage(); key.Disposed += (_, _) => value.Image?.Dispose(); return value;
        });
        var bitmap = glyph is "exit" or "clock" or "appearance" or "folder" or "help" or "about" or "report"
            ? CommonMenuGlyphs.Create(glyph, color) : CreateLocal(glyph, color);
        var old = owner.Image; owner.Image = bitmap; item.Image = bitmap; old?.Dispose();
    }
    static Bitmap CreateLocal(string glyph, Color color)
    {
        var bitmap = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bitmap))
        using (var pen = new Pen(color, 1.3f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            switch (glyph)
            {
                case "active-dot":
                    using (var fill = new SolidBrush(color)) g.FillEllipse(fill, 3.5f, 3.5f, 9, 9); break;
                case "warning":
                    using (var heavy = new Pen(color, 1.9f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
                    {
                        g.DrawPolygon(heavy, [new PointF(8, 1.5f), new(15, 14), new(1, 14)]);
                        g.DrawLine(heavy, 8, 5.5f, 8, 9);
                    }
                    using (var fill = new SolidBrush(color)) g.FillEllipse(fill, 7, 11, 2, 2);
                    break;
                case "resume":
                    g.DrawPolygon(pen, [new Point(5, 3), new(13, 8), new(5, 13)]); break;
                case "save":
                    g.DrawRectangle(pen, 2, 2, 12, 12); g.DrawRectangle(pen, 5, 2, 6, 4); g.DrawRectangle(pen, 5, 9, 6, 5); break;
                case "close":
                    g.DrawLine(pen, 4, 4, 12, 12); g.DrawLine(pen, 12, 4, 4, 12); break;
                case "pause":
                    g.DrawRectangle(pen, 4, 3, 2, 10); g.DrawRectangle(pen, 10, 3, 2, 10); break;
                default:
                    for (int i = 0; i < 3; i++) { int y = 4 + i * 4; g.DrawLine(pen, 2, y, 14, y); g.DrawEllipse(pen, i % 2 == 0 ? 5 : 9, y - 1.5f, 3, 3); } break;
            }
        }
        return bitmap;
    }
}
