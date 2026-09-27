using System;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.TextFormatting;
using AvaloniaEdit.Rendering;
using TT_Lab.Controls;

namespace TT_Lab.Views.Editors.Global;

/// <summary>
/// Draws a text's characters with a PSF font's glyphs as tall as the editor's lines, the characters the font has no glyph of stay text
/// </summary>
internal sealed class PsfGlyphGenerator(PsfGlyphs glyphs) : VisualLineElementGenerator
{
    // The game starts a new line on it instead of drawing it
    public const char LineBreak = '~';

    private static readonly IBrush LineBreakBrush = new SolidColorBrush(Color.FromRgb(0x5C, 0xA8, 0xFF));

    private double _scale = 1.0;
    private double _baseline;

    // Lines keep the height of the editor's text, glyphs hang from the top of theirs
    public override void StartGeneration(ITextRunConstructionContext context)
    {
        base.StartGeneration(context);
        var properties = context.GlobalTextRunProperties;
        var line = new FormattedText("X", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, properties.Typeface, properties.FontRenderingEmSize, null);
        _scale = glyphs.LineHeight > 0 ? line.Height / glyphs.LineHeight : 1.0;
        _baseline = line.Baseline;
    }

    public override int GetFirstInterestedOffset(int startOffset)
    {
        var document = CurrentContext.Document;
        var end = CurrentContext.VisualLine.LastDocumentLine.EndOffset;
        for (var offset = startOffset; offset < end; offset++)
        {
            var character = document.GetCharAt(offset);
            if (character == LineBreak || glyphs.TryGetGlyph(character, out _))
            {
                return offset;
            }
        }

        return -1;
    }

    public override VisualLineElement ConstructElement(int offset)
    {
        var character = CurrentContext.Document.GetCharAt(offset);
        if (character == LineBreak)
        {
            return new LineBreakElement();
        }

        glyphs.TryGetGlyph(character, out var glyph);
        return new GlyphElement(glyph.Image, glyph.Size * _scale, _baseline);
    }

    private sealed class GlyphElement(Bitmap? image, Size size, double baseline) : VisualLineElement(1, 1)
    {
        public override TextRun CreateTextRun(int startVisualColumn, ITextRunConstructionContext context)
        {
            return new DrawnRun(size, baseline, TextRunProperties, (drawingContext, origin) =>
            {
                if (image != null)
                {
                    drawingContext.DrawImage(image, new Rect(origin, size));
                }
            });
        }
    }

    private sealed class LineBreakElement() : VisualLineElement(1, 1)
    {
        public override TextRun CreateTextRun(int startVisualColumn, ITextRunConstructionContext context)
        {
            var text = new FormattedText("↵", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, TextRunProperties.Typeface, TextRunProperties.FontRenderingEmSize,
                LineBreakBrush);
            return new DrawnRun(new Size(text.WidthIncludingTrailingWhitespace, text.Height), text.Baseline, TextRunProperties,
                (drawingContext, origin) => drawingContext.DrawText(text, origin));
        }
    }

    private sealed class DrawnRun(Size size, double baseline, TextRunProperties properties, Action<DrawingContext, Point> draw) : DrawableTextRun
    {
        public override Size Size => size;

        public override double Baseline => baseline;

        public override TextRunProperties Properties => properties;

        public override void Draw(DrawingContext drawingContext, Point origin) => draw(drawingContext, origin);
    }
}
