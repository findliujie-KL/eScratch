using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace eScratch;

public static class ThemeTrayIcon
{
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);
    public static Icon Create(string theme, bool light)
    {
        var palette = PaperThemes.Palette(theme, light);
        using var bitmap = new Bitmap(32, 32);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var paper = new SolidBrush(ColorTranslator.FromHtml(palette["Surface"]));
            using var fold = new SolidBrush(ColorTranslator.FromHtml(palette["Panel"]));
            using var edge = new Pen(ColorTranslator.FromHtml(palette["Secondary"]), 1.5f);
            using var ink = new Pen(ColorTranslator.FromHtml(palette["Accent"]), 1.5f);
            graphics.Clear(Color.Transparent);
            // Opposite appearance provides a contrasting, theme-tinted backing.
            using var backing = new SolidBrush(ColorTranslator.FromHtml(PaperThemes.Palette(theme, !light)["Surface"]));
            using var tile = new GraphicsPath();
            tile.AddArc(0, 0, 14, 14, 180, 90);
            tile.AddArc(18, 0, 14, 14, 270, 90);
            tile.AddArc(18, 18, 14, 14, 0, 90);
            tile.AddArc(0, 18, 14, 14, 90, 90);
            tile.CloseFigure();
            graphics.FillPath(backing, tile);
            graphics.TranslateTransform(16, 16);
            graphics.RotateTransform(-10);
            graphics.ScaleTransform(.72f, .72f);
            graphics.TranslateTransform(-16, -16);
            var shape = new[] { new PointF(5, 3), new PointF(21, 3), new PointF(27, 9), new PointF(27, 29), new PointF(5, 29) };
            graphics.FillPolygon(paper, shape);
            graphics.DrawPolygon(edge, shape);
            var corner = new[] { new PointF(21, 3), new PointF(21, 9), new PointF(27, 9) };
            graphics.FillPolygon(fold, corner);
            graphics.DrawLines(edge, corner);
            graphics.DrawLine(ink, 10, 14, 22, 14);
            graphics.DrawLine(ink, 10, 19, 22, 19);
            graphics.DrawLine(ink, 10, 24, 18, 24);
        }
        var handle = bitmap.GetHicon();
        try { using var temporary = Icon.FromHandle(handle); return (Icon)temporary.Clone(); }
        finally { DestroyIcon(handle); }
    }
}
