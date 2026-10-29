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
internal sealed class FlatMenuRenderer(UiColors colors):ToolStripProfessionalRenderer(new MenuPalette(colors))
{
    static bool IsStatus(ToolStripItem item)=>item.Tag is string tag&&tag.StartsWith("status-",StringComparison.Ordinal);
    static Color SelectionColor(UiColors value)=>SystemInformation.HighContrast?SystemColors.Highlight:ColorTranslator.FromHtml(value.Dark?"#414141":"#EEEEEE");
    internal static void ApplySpacing(ToolStrip strip)
    {
        int Scale(int value)=>(int)Math.Round(value*strip.DeviceDpi/96f);
        strip.Padding=strip is ToolStripDropDown?new Padding(Scale(6)):new Padding(Scale(10),Scale(4),0,Scale(4));
        strip.ImageScalingSize=new Size(Scale(24),Scale(18));
        foreach(ToolStripItem item in strip.Items)
        {
            if(item is ToolStripSeparator)continue;
            item.Padding=strip is ToolStripDropDown?new Padding(Scale(12),Scale(9),Scale(20),Scale(9)):new Padding(Scale(7),Scale(3),Scale(7),Scale(3));
            if(item is ToolStripMenuItem entry&&entry.HasDropDownItems)ApplySpacing(entry.DropDown);
        }
    }
    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        e.Graphics.Clear(SystemInformation.HighContrast?SystemColors.Menu:e.ToolStrip.BackColor);
    }
    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }
    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        if(e.ToolStrip is not ToolStripDropDown dropdown)return;
        Color border=SystemInformation.HighContrast?SystemColors.WindowText:ControlPaint.Light(e.ToolStrip?.BackColor??ColorTable.ToolStripDropDownBackground,.18f);
        using var pen=new Pen(border);
        e.Graphics.DrawRectangle(pen,0,0,dropdown.Width-1,dropdown.Height-1);
    }
    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if(IsStatus(e.Item)||!e.Item.Enabled||!(e.Item.Selected||e.Item.Pressed))return;
        bool highContrast=SystemInformation.HighContrast;
        using var fill=new SolidBrush(highContrast?SystemColors.Highlight:ColorTable.MenuItemSelected);
        float scale=(e.ToolStrip?.DeviceDpi??96)/96f;
        var bounds=new RectangleF(2*scale,scale,VisibleItemWidth(e.Item)-4*scale,e.Item.Height-2*scale);
        if(bounds.Width<=0||bounds.Height<=0)return;
        var state=e.Graphics.Save();
        e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        using var shape=Drawing.Rounded(bounds,4*scale);
        e.Graphics.FillPath(fill,shape);
        e.Graphics.Restore(state);
    }
    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        float scale=(e.ToolStrip?.DeviceDpi??96)/96f;
        Color color=SystemInformation.HighContrast?SystemColors.WindowText:ControlPaint.Light(e.ToolStrip?.BackColor??ColorTable.ToolStripDropDownBackground,.18f);
        using var pen=new Pen(color);
        float y=e.Item.Height/2f;
        e.Graphics.DrawLine(pen,9*scale,y,VisibleItemWidth(e.Item)-9*scale,y);
    }
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        if(IsStatus(e.Item))e.TextColor=e.Item.ForeColor;
        else if(!e.Item.Enabled)e.TextColor=SystemInformation.HighContrast?SystemColors.GrayText:ControlPaint.Light(e.Item.ForeColor,.45f);
        else if(SystemInformation.HighContrast)e.TextColor=e.Item.Selected||e.Item.Pressed?SystemColors.HighlightText:SystemColors.MenuText;
        float scale=(e.ToolStrip?.DeviceDpi??96)/96f;
        var text=e.TextRectangle;
        if(e.ToolStrip is ToolStripDropDown)
        {
            int left=(int)Math.Round(46*scale),right=(int)Math.Round(20*scale);
            text=new Rectangle(left,0,Math.Max(1,VisibleItemWidth(e.Item)-left-right),e.Item.Height);
        }
        else text=new Rectangle(text.X,0,text.Width,e.Item.Height);
        e.TextRectangle=text;
        e.TextFormat=(e.TextFormat&~(TextFormatFlags.Top|TextFormatFlags.Bottom))|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine;
        if(IsStatus(e.Item))TextRenderer.DrawText(e.Graphics,e.Text,e.TextFont,e.TextRectangle,e.TextColor,e.TextFormat);
        else base.OnRenderItemText(e);
    }
    static int VisibleItemWidth(ToolStripItem item)=>Math.Min(item.Width,(item.Owner?.ClientSize.Width??item.Width)-item.Bounds.Left);
    static Rectangle GlyphBounds(ToolStripItem item)
    {
        float scale=(item.Owner?.DeviceDpi??96)/96f;
        int size=(int)Math.Round(18*scale);
        return new Rectangle((int)Math.Round(14*scale),(item.Height-size)/2,size,size);
    }
    protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
    {
        if(e.Image is null)return;
        var bounds=GlyphBounds(e.Item);
        if(!e.Item.Enabled&&!IsStatus(e.Item))
        {
            using var disabled=new Bitmap(e.Image,bounds.Size);
            ControlPaint.DrawImageDisabled(e.Graphics,disabled,bounds.X,bounds.Y,e.Item.BackColor);
            return;
        }
        var state=e.Graphics.Save();
        e.Graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;
        e.Graphics.PixelOffsetMode=PixelOffsetMode.HighQuality;
        e.Graphics.DrawImage(e.Image,bounds);
        e.Graphics.Restore(state);
    }
    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var bounds=GlyphBounds(e.Item);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        using var pen=new Pen(SystemInformation.HighContrast?(e.Item.Selected?SystemColors.HighlightText:SystemColors.MenuText):colors.Accent,2);
        float unit=Math.Max(1,Math.Min(bounds.Width,bounds.Height)/16f);
        float x=bounds.Left+(bounds.Width-11*unit)/2,y=bounds.Top+bounds.Height/2f;
        e.Graphics.DrawLines(pen,[new PointF(x,y),new PointF(x+4*unit,y+4*unit),new PointF(x+11*unit,y-4*unit)]);
    }
    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        var bounds=e.ArrowRectangle;float x=bounds.Left+bounds.Width/2f,y=(e.Item?.Height??bounds.Height)/2f;
        if(e.Item is not null)x=Math.Min(x,VisibleItemWidth(e.Item)-12*(e.Item.Owner?.DeviceDpi??96)/96f);
        float size=Math.Max(2,Math.Min(bounds.Width,bounds.Height)/4f);
        var color=e.Item is {Enabled:false}?SystemColors.GrayText:SystemInformation.HighContrast?(e.Item?.Selected==true?SystemColors.HighlightText:SystemColors.MenuText):e.Item?.ForeColor??e.ArrowColor;
        using var pen=new Pen(color,1.5f);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        PointF[] points=e.Direction switch
        {
            ArrowDirection.Left=>[new(x+size/2,y-size),new(x-size/2,y),new(x+size/2,y+size)],
            ArrowDirection.Up=>[new(x-size,y+size/2),new(x,y-size/2),new(x+size,y+size/2)],
            ArrowDirection.Down=>[new(x-size,y-size/2),new(x,y+size/2),new(x+size,y-size/2)],
            _=>[new(x-size/2,y-size),new(x+size/2,y),new(x-size/2,y+size)]
        };
        e.Graphics.DrawLines(pen,points);
    }
    sealed class MenuPalette(UiColors p) : ProfessionalColorTable
    {
        public override Color CheckBackground => p.Surface;
        public override Color CheckSelectedBackground => SelectionColor(p);
        public override Color CheckPressedBackground => SelectionColor(p);
        public override Color ToolStripDropDownBackground => p.Surface;
        public override Color ImageMarginGradientBegin => p.Surface;
        public override Color ImageMarginGradientMiddle => p.Surface;
        public override Color ImageMarginGradientEnd => p.Surface;
        public override Color MenuBorder => p.Border;
        public override Color MenuItemBorder => SelectionColor(p);
        public override Color MenuItemSelected => SelectionColor(p);
        public override Color MenuItemSelectedGradientBegin => SelectionColor(p);
        public override Color MenuItemSelectedGradientEnd => SelectionColor(p);
        public override Color MenuItemPressedGradientBegin => SelectionColor(p);
        public override Color MenuItemPressedGradientMiddle => SelectionColor(p);
        public override Color MenuItemPressedGradientEnd => SelectionColor(p);
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
        FlatMenuRenderer.ApplySpacing(strip);
    }
}


// Locally drawn line glyphs; no application artwork or other project's assets are embedded.
internal static class MenuSymbols
{
    sealed class OwnedImage { internal Bitmap? Image; }
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ToolStripItem, OwnedImage> images = new();
    internal static void Apply(ToolStripItem item, Color color)
    {
        if (!(item.Tag is string statusTag && statusTag.StartsWith("status-", StringComparison.Ordinal)) && !SystemInformation.HighContrast)
        {
            bool dark = item.Owner is not null ? item.Owner.BackColor.GetBrightness() < .5f : color.GetBrightness() > .5f;
            color = ColorTranslator.FromHtml(dark ? "#D2D2D2" : "#4B5563");
        }

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
        var bitmap = new Bitmap(64, 64);
        using (var g = Graphics.FromImage(bitmap))
        using (var pen = new Pen(color, 1.35f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
        {
            g.ScaleTransform(4, 4); g.SmoothingMode = SmoothingMode.AntiAlias;
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
