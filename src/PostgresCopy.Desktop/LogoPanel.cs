// File: src/PostgresCopy.Desktop/LogoPanel.cs

using System.Drawing.Drawing2D;
using System.Reflection;

namespace PostgresCopy.Desktop;

public sealed class LogoPanel : Control
{
    private static readonly Lazy<LogoAsset?> Logo = new(LoadEmbeddedLogo);

    public LogoPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.UserPaint
            | ControlStyles.ResizeRedraw
            | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Size = new Size(156, 156);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var logo = Logo.Value;
        if (logo is null) return;

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        var side = Math.Min(Width, Height);
        var padding = Math.Max(0, side * 0.02f);
        var target = new RectangleF(
            (Width - side) / 2f + padding,
            (Height - side) / 2f + padding,
            side - padding * 2,
            side - padding * 2);

        g.DrawImage(logo.Image, target, logo.ContentBounds, GraphicsUnit.Pixel);
    }

    private static LogoAsset? LoadEmbeddedLogo()
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("kitsunedb.png", StringComparison.Ordinal));
        if (name is null) return null;

        using var stream = asm.GetManifestResourceStream(name);
        if (stream is null) return null;

        // Copy to memory so the Image owns its data after the stream is disposed.
        var ms = new MemoryStream();
        stream.CopyTo(ms);
        ms.Position = 0;
        var image = Image.FromStream(ms);
        return new LogoAsset(image, FindContentBounds(image));
    }

    private static Rectangle FindContentBounds(Image image)
    {
        using var bitmap = new Bitmap(image);
        var minX = bitmap.Width;
        var minY = bitmap.Height;
        var maxX = -1;
        var maxY = -1;

        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).A <= 8)
                    continue;

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        if (maxX < minX || maxY < minY)
            return new Rectangle(0, 0, image.Width, image.Height);

        var content = Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
        var side = Math.Max(content.Width, content.Height);
        var centerX = content.Left + content.Width / 2;
        var centerY = content.Top + content.Height / 2;
        var left = Math.Max(0, centerX - side / 2);
        var top = Math.Max(0, centerY - side / 2);

        if (left + side > image.Width)
            left = Math.Max(0, image.Width - side);
        if (top + side > image.Height)
            top = Math.Max(0, image.Height - side);

        return new Rectangle(left, top, Math.Min(side, image.Width - left), Math.Min(side, image.Height - top));
    }

    private sealed record LogoAsset(Image Image, Rectangle ContentBounds);
}
