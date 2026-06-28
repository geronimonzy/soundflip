using System.Drawing;
using System.Drawing.Imaging;

internal readonly record struct StoreAssetSpec(string FileName, int Width, int Height, bool Wordmark);

static class StoreAssetExporter
{
    public static IReadOnlyList<StoreAssetSpec> AssetPlan { get; } =
    [
        new StoreAssetSpec("Square44x44Logo.png", 44, 44, false),
        new StoreAssetSpec("StoreLogo.png", 50, 50, false),
        new StoreAssetSpec("Square150x150Logo.png", 150, 150, false),
        new StoreAssetSpec("Wide310x150Logo.png", 310, 150, true),
        new StoreAssetSpec("Square310x310Logo.png", 310, 310, true),
    ];

    public static void Export(string directory)
    {
        Directory.CreateDirectory(directory);

        foreach (var asset in AssetPlan)
            Write(directory, asset);
    }

    static void Write(string directory, StoreAssetSpec asset)
    {
        using var bitmap = Create(asset.Width, asset.Height, asset.Wordmark);
        bitmap.Save(Path.Combine(directory, asset.FileName), ImageFormat.Png);
    }

    static Bitmap Create(int width, int height, bool wordmark)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        g.Clear(Color.Transparent);

        var card = wordmark
            ? new RectangleF(width * 0.06F, height * 0.18F, width * 0.88F, height * 0.64F)
            : new RectangleF(width * 0.12F, height * 0.12F, width * 0.76F, height * 0.76F);

        using (var fill = new SolidBrush(Theme.Accent))
        using (var path = Gfx.Round(card, MathF.Min(card.Width, card.Height) * 0.18F))
            g.FillPath(fill, path);

        if (wordmark)
        {
            var iconRect = new RectangleF(card.Left + card.Height * 0.16F, card.Top + card.Height * 0.18F, card.Height * 0.64F, card.Height * 0.64F);
            TrayArt.DrawSpeaker(g, iconRect, Color.White);

            using var font = new Font("Segoe UI Semibold", height * 0.25F, GraphicsUnit.Pixel);
            using var brush = new SolidBrush(Color.White);
            using var format = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
            var textRect = new RectangleF(iconRect.Right + card.Height * 0.16F, card.Top, card.Right - iconRect.Right - card.Height * 0.24F, card.Height);
            g.DrawString("audsw", font, brush, textRect, format);
            return bitmap;
        }

        var squareIcon = new RectangleF(card.Left + card.Width * 0.22F, card.Top + card.Height * 0.16F, card.Width * 0.56F, card.Height * 0.56F);
        TrayArt.DrawSpeaker(g, squareIcon, Color.White);

        if (width >= 120)
        {
            using var font = new Font("Segoe UI Semibold", Math.Min(width * 0.12F, 34F), GraphicsUnit.Pixel);
            using var brush = new SolidBrush(Color.White);
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            var textRect = new RectangleF(card.Left, card.Bottom - card.Height * 0.25F, card.Width, card.Height * 0.18F);
            g.DrawString("audsw", font, brush, textRect, format);
        }

        return bitmap;
    }
}
