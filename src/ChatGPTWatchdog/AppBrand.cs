using System.Drawing.Drawing2D;
using System.Reflection;

namespace ChatGPTWatchdog;

internal static class AppBrand
{
    internal static Icon LoadIcon(int size)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("watchdog.ico")
            ?? throw new InvalidDataException("Application icon is missing.");
        using var buffer = new MemoryStream(); stream.CopyTo(buffer);
        byte[] bytes = buffer.ToArray();
        int count = BitConverter.ToUInt16(bytes, 4);
        // Explicitly prefer the supplied 32-bit frame over legacy 4/8-bit alternatives.
        var selected = Enumerable.Range(0, count).Select(index =>
        {
            int entry = 6 + index * 16;
            int width = bytes[entry] == 0 ? 256 : bytes[entry];
            int height = bytes[entry + 1] == 0 ? 256 : bytes[entry + 1];
            return new { Entry = entry, Width = width, Height = height, Bits = BitConverter.ToUInt16(bytes, entry + 6),
                Length = BitConverter.ToInt32(bytes, entry + 8), Offset = BitConverter.ToInt32(bytes, entry + 12) };
        }).OrderBy(frame => Math.Abs(frame.Width - size) + Math.Abs(frame.Height - size))
          .ThenByDescending(frame => frame.Bits).First();
        byte[] frame = new byte[22 + selected.Length];
        Array.Copy(bytes, 0, frame, 0, 6);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(4), 1);
        Array.Copy(bytes, selected.Entry, frame, 6, 16);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(18), 22);
        Array.Copy(bytes, selected.Offset, frame, 22, selected.Length);
        using var isolated = new MemoryStream(frame);
        using var source = new Icon(isolated, new Size(selected.Width, selected.Height));
        return (Icon)source.Clone();
    }
    internal static Icon LoadPausedIcon(int size)
    {
        using var original = LoadIcon(size);
        using var bitmap = original.ToBitmap();
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float cx = bitmap.Width * .78f, cy = bitmap.Height * .78f, radius = bitmap.Width * .18f;
            using var fill = new SolidBrush(Color.FromArgb(255, 194, 73));
            graphics.FillEllipse(fill, cx - radius, cy - radius, radius * 2, radius * 2);
        }
        nint handle = bitmap.GetHicon();
        try { using var borrowed = Icon.FromHandle(handle); return (Icon)borrowed.Clone(); }
        finally { DestroyIcon(handle); }
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    static extern bool DestroyIcon(nint icon);

    internal static Bitmap LoadArtwork()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("watchdog.png")
            ?? throw new InvalidDataException("Application artwork is missing.");
        using var source = new Bitmap(stream);
        return new Bitmap(source);
    }
}

internal sealed class BrandPicture : PictureBox
{
    internal BrandPicture() { Image = AppBrand.LoadArtwork(); SizeMode = PictureBoxSizeMode.Zoom; TabStop = false; }
    protected override void OnPaint(PaintEventArgs e)
    {
        if (Image is null) return;
        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        float scale = Math.Min(ClientSize.Width / (float)Image.Width, ClientSize.Height / (float)Image.Height);
        var size = new SizeF(Image.Width * scale, Image.Height * scale);
        e.Graphics.DrawImage(Image, new RectangleF((ClientSize.Width - size.Width) / 2, (ClientSize.Height - size.Height) / 2, size.Width, size.Height));
    }
    protected override void Dispose(bool disposing) { if (disposing) Image?.Dispose(); base.Dispose(disposing); }
}

