using System.Drawing.Drawing2D;
using System.Reflection;

namespace PostgresCopy.Desktop;

public sealed class LogoPanel : Control
{
    private static readonly Lazy<Image?> Logo = new(LoadEmbeddedLogo);

    public LogoPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.UserPaint
            | ControlStyles.ResizeRedraw
            | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Size = new Size(160, 60);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var image = Logo.Value;
        if (image is null) return;

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        var scale = Math.Min((float)Width / image.Width, (float)Height / image.Height);
        var w = image.Width * scale;
        var h = image.Height * scale;
        var x = (Width - w) / 2f;
        var y = (Height - h) / 2f;

        g.DrawImage(image, x, y, w, h);
    }

    private static Image? LoadEmbeddedLogo()
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("kabuki-db-jump.png", StringComparison.Ordinal));
        if (name is null) return null;

        using var stream = asm.GetManifestResourceStream(name);
        if (stream is null) return null;

        // Copy to memory so the Image owns its data after the stream is disposed.
        var ms = new MemoryStream();
        stream.CopyTo(ms);
        ms.Position = 0;
        return Image.FromStream(ms);
    }
}
