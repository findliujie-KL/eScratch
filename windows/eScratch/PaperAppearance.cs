using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Rendering;

namespace eScratch;

public static class PaperThemes
{
    public static readonly string[] Names = { "Porcelain", "Book paper", "Sage notebook", "Drafting paper" };
    public static Dictionary<string, string> Palette(string name, bool light)
    {
        string[] colors;
        switch (name)
        {
            case "Book paper": colors = light ? new[] { "#f6efdf", "#eae1ce", "#493f32", "#79694f", "#96612e", "#d5c6a9" } : new[] { "#28241f", "#332d26", "#ede1cf", "#baaa94", "#e2b780", "#4c4033" }; break;
            case "Sage notebook": colors = light ? new[] { "#eff3e9", "#e0e8d8", "#34483a", "#5e735d", "#47775b", "#c3d1bb" } : new[] { "#202b25", "#29372e", "#dde9da", "#a4bda8", "#9dcca4", "#405747" }; break;
            case "Drafting paper": colors = light ? new[] { "#eff5f8", "#dde9f0", "#304859", "#607889", "#326e99", "#9bb7c9" } : new[] { "#1b2935", "#243746", "#dfebf4", "#a2bbce", "#8fc8ed", "#54768e" }; break;
            default: colors = light ? new[] { "#f4f5f9", "#e8eaf2", "#343a50", "#626b85", "#596bb0", "#d4d8e5" } : new[] { "#20212b", "#292b38", "#e0e2ed", "#a8aec6", "#b0baff", "#393c50" }; break;
        }
        return new Dictionary<string, string> { ["Surface"] = colors[0], ["Panel"] = colors[1], ["Ink"] = colors[2], ["Secondary"] = colors[3], ["Muted"] = colors[3], ["Accent"] = colors[4], ["Line"] = colors[5], ["Hover"] = colors[5], ["Active"] = colors[5] };
    }
}

// Draw behind the editor. Rules use the actual formatted rows, including wrapped text.
public sealed class PaperSurface : FrameworkElement
{
    private TextEditor? editor;
    private Settings? settings;
    public void Attach(TextEditor value, Settings options)
    {
        editor = value; settings = options;
        // LayoutUpdated also fires after our own drawing. Invalidating there can
        // create an endless arrange/render cycle, even for a hidden scratch.
        value.TextArea.TextView.VisualLinesChanged += (_, _) => { if (settings?.PaperTheme is "Sage notebook" or "Drafting paper") InvalidateVisual(); };
        value.TextArea.TextView.ScrollOffsetChanged += (_, _) => { if (settings?.PaperTheme == "Sage notebook") InvalidateVisual(); };
        value.SizeChanged += (_, _) => InvalidateVisual();
        IsHitTestVisible = false;
    }
    public void Update(Settings options) { settings = options; InvalidateVisual(); }
    protected override void OnRender(DrawingContext dc)
    {
        if (editor == null || settings == null || settings.PaperTheme == "Porcelain" || settings.TextureIntensity <= 0) return;
        var brush = ((SolidColorBrush)FindResource("Line")).Clone();
        brush.Opacity = settings.TextureIntensity / 100.0;
        var pen = new Pen(brush, 1);
        dc.PushClip(new RectangleGeometry(new Rect(RenderSize)));
        if (settings.PaperTheme == "Sage notebook")
        {
            var view = editor.TextArea.TextView;
            if (view.VisualLinesValid)
            {
                var origin = view.TranslatePoint(new Point(), this);
                var last = origin.Y - view.VerticalOffset;
                foreach (var line in view.VisualLines)
                    foreach (var row in line.TextLines)
                    {
                        // The bottom includes font descent, so the rule sits below the glyphs.
                        var y = origin.Y + line.GetTextLineVisualYPosition(row, VisualYPosition.LineBottom) - view.VerticalOffset;
                        dc.DrawLine(pen, new Point(0, y), new Point(ActualWidth, y)); last = y;
                    }
                var spacing = Math.Max(1, view.DefaultLineHeight);
                for (var y = last + spacing; y < ActualHeight; y += spacing)
                    if (y >= 0) dc.DrawLine(pen, new Point(0, y), new Point(ActualWidth, y));
            }
        }
        else if (settings.PaperTheme == "Drafting paper")
        {
            var spacing = Math.Max(12, editor.FontSize * 1.1);
            for (double y = 20; y < ActualHeight; y += spacing)
                for (double x = 20; x < ActualWidth; x += spacing) dc.DrawEllipse(brush, null, new Point(x, y), .65, .65);
        }
        else
        {
            // Repeatable fine fibers; no image files or random flicker between frames.
            var fibers = new DrawingGroup();
            using (var tile = fibers.Open())
            {
                tile.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, 64, 64));
                var random = new Random(37);
                for (var i = 0; i < 75; i++)
                {
                    var x = random.NextDouble() * 61; var y = random.NextDouble() * 63;
                    tile.DrawLine(pen, new Point(x, y), new Point(x + 1 + random.NextDouble() * 2, y + .4));
                }
            }
            var texture = new DrawingBrush(fibers) { TileMode = TileMode.Tile, ViewportUnits = BrushMappingMode.Absolute, Viewport = new Rect(0, 0, 64, 64) };
            dc.DrawRectangle(texture, null, new Rect(RenderSize));
        }
        dc.Pop();
    }
}
