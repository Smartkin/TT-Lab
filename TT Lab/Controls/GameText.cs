using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace TT_Lab.Controls;

/// <summary>
/// A line of text drawn with one of the game's fonts, its glyphs as tall as the line and the whole line made smaller when it doesn't fit.
/// What the font has no glyph of, and what it draws as a controller button, is drawn with the application's font
/// </summary>
public sealed class GameText : Control
{
    public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<GameText, string?>(nameof(Text));

    public static readonly StyledProperty<PsfGlyphs?> GlyphsProperty = AvaloniaProperty.Register<GameText, PsfGlyphs?>(nameof(Glyphs));

    public static readonly StyledProperty<double> LineHeightProperty = AvaloniaProperty.Register<GameText, double>(nameof(LineHeight), 32.0);

    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<GameText>();

    // The application's font is smaller than a line of the game's, whose glyphs have room for an outline
    private const double FallbackSize = 0.7;

    private double _fit = 1.0;

    static GameText()
    {
        AffectsMeasure<GameText>(TextProperty, GlyphsProperty, LineHeightProperty);
        AffectsRender<GameText>(TextProperty, GlyphsProperty, LineHeightProperty, ForegroundProperty);
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public PsfGlyphs? Glyphs
    {
        get => GetValue(GlyphsProperty);
        set => SetValue(GlyphsProperty, value);
    }

    public double LineHeight
    {
        get => GetValue(LineHeightProperty);
        set => SetValue(LineHeightProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    private readonly record struct Piece(double Width, double Height, Bitmap? Image, FormattedText? Text);

    /// <summary>
    /// The characters as they're drawn at the line's height, left to right
    /// </summary>
    internal IReadOnlyList<(char Character, double Width, bool IsGlyph)> Layout()
    {
        var pieces = new List<(char, double, bool)>();
        foreach (var piece in Pieces())
        {
            pieces.Add((piece.Character, piece.Piece.Width, piece.Piece.Text == null));
        }

        return pieces;
    }

    private IEnumerable<(char Character, Piece Piece)> Pieces()
    {
        var text = Text ?? string.Empty;
        var glyphs = Glyphs;
        var lineHeight = LineHeight;
        var scale = glyphs is { LineHeight: > 0 } ? lineHeight / glyphs.LineHeight : 1.0;
        foreach (var character in text)
        {
            if (glyphs != null && TryGetGlyph(glyphs, character, out var glyph))
            {
                yield return (character, new Piece(glyph.Size.Width * scale, glyph.Size.Height * scale, glyph.Image, null));
                continue;
            }

            var formatted = new FormattedText(character.ToString(), CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Typeface.Default,
                Math.Max(lineHeight * FallbackSize, 1.0), Foreground ?? Brushes.White);
            yield return (character, new Piece(formatted.WidthIncludingTrailingWhitespace, formatted.Height, null, formatted));
        }
    }

    // The game's texts spell words in lower case, which its font draws as small capitals: its capital C and E are controller buttons
    private static bool TryGetGlyph(PsfGlyphs glyphs, char character, out PsfGlyphs.Glyph glyph)
    {
        if (!PsfGlyphs.ButtonCharacters.Contains(character) && glyphs.TryGetGlyph(character, out glyph))
        {
            return true;
        }

        var otherCase = char.IsUpper(character) ? char.ToLowerInvariant(character) : char.ToUpperInvariant(character);
        glyph = null!;
        return otherCase != character && !PsfGlyphs.ButtonCharacters.Contains(otherCase) && glyphs.TryGetGlyph(otherCase, out glyph);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = 0.0;
        foreach (var (_, piece) in Pieces())
        {
            width += piece.Width;
        }

        _fit = width > availableSize.Width && width > 0.0 ? availableSize.Width / width : 1.0;
        return new Size(width * _fit, LineHeight * _fit);
    }

    public override void Render(DrawingContext context)
    {
        var lineHeight = LineHeight;
        using var fit = context.PushTransform(Matrix.CreateScale(_fit, _fit));
        var x = 0.0;
        foreach (var (_, piece) in Pieces())
        {
            if (piece.Image != null)
            {
                context.DrawImage(piece.Image, new Rect(x, 0.0, piece.Width, piece.Height));
            }
            else if (piece.Text != null)
            {
                context.DrawText(piece.Text, new Point(x, (lineHeight - piece.Height) / 2.0));
            }

            x += piece.Width;
        }
    }
}
