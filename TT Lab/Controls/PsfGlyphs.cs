using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using TT_Lab.AssetData.Global;
using TT_Lab.AssetData.Graphics;
using TT_Lab.Assets;

namespace TT_Lab.Controls;

/// <summary>
/// Characters of a PSF font as the game draws them, some of them are controller buttons
/// </summary>
/// <remarks>
/// The table starts at the space character. A character's box goes up from its bottom left corner on its page, so the page has it upside
/// down. Page 0 and 1 are the first page, 2 the second and so on: the PS2 fonts number pages from 1 and give characters without a glyph 0,
/// the Xbox's use 0 and 1 for the first page. Characters without a size have no glyph, the space has a size and nothing on its page
/// </remarks>
public sealed class PsfGlyphs
{
    /// <summary>
    /// What the game's texts use for controller buttons, the fonts of both versions of the game draw their own buttons for them
    /// </summary>
    public const string ButtonCharacters = "[\\]^{}¦¬<>#CEÎ";

    public sealed record Glyph(char Character, Bitmap? Image, Size Size);

    private readonly Dictionary<char, Glyph> _glyphs = [];

    private PsfGlyphs()
    {
    }

    public double LineHeight { get; private init; }

    public int Count => _glyphs.Count;

    public static PsfGlyphs FromFont(FontData font)
    {
        var assetManager = AssetManager.Get();
        var pages = font.FontPages.Select(page =>
        {
            var texture = assetManager.GetAssetData<TextureData>(assetManager.GetAssetData<PTCData>(page).TextureID);
            return (Pixels: texture.GetPixels(), texture.Bitmap!.PixelSize);
        }).ToList();

        var glyphs = new PsfGlyphs { LineHeight = font.CharacterData.Select(character => (double)character.Size.Y).DefaultIfEmpty(0).Max() };
        for (var i = 0; i < font.CharacterData.Count; i++)
        {
            var character = font.CharacterData[i];
            var code = font.SpaceIdentifier + i;
            var width = (int)MathF.Round(character.Size.X);
            var height = (int)MathF.Round(character.Size.Y);
            if (code > Char.MaxValue || width <= 0 || height <= 0)
            {
                continue;
            }

            var page = Math.Max(character.FontPageSpecifier - 1, 0);
            var image = page < pages.Count ? Crop(pages[page].Pixels, pages[page].PixelSize, (int)MathF.Floor(character.PageUv.X), (int)MathF.Floor(character.PageUv.Y), width, height) : null;
            glyphs._glyphs[(char)code] = new Glyph((char)code, image, new Size(width, height));
        }

        return glyphs;
    }

    public bool TryGetGlyph(char character, out Glyph glyph) => _glyphs.TryGetValue(character, out glyph!);

    /// <summary>
    /// Every character the font draws, in the order of their codes
    /// </summary>
    public IEnumerable<char> Characters => _glyphs.Keys.OrderBy(character => character);

    // The box's bottom row is the glyph's top one
    private static Bitmap? Crop(UInt32[] pixels, PixelSize pageSize, int left, int bottom, int width, int height)
    {
        var glyph = new UInt32[width * height];
        var anyDrawn = false;
        for (var row = 0; row < height; row++)
        {
            var pageRow = bottom - 1 - row;
            for (var column = 0; column < width; column++)
            {
                var pageColumn = left + column;
                if (pageRow < 0 || pageRow >= pageSize.Height || pageColumn < 0 || pageColumn >= pageSize.Width)
                {
                    continue;
                }

                var pixel = pixels[pageRow * pageSize.Width + pageColumn];
                glyph[row * width + column] = pixel;
                anyDrawn |= pixel >> 24 != 0;
            }
        }

        if (!anyDrawn)
        {
            return null;
        }

        var handle = GCHandle.Alloc(glyph, GCHandleType.Pinned);
        try
        {
            return new Bitmap(PixelFormat.Bgra8888, AlphaFormat.Unpremul, handle.AddrOfPinnedObject(), new PixelSize(width, height), new Vector(96, 96), width * 4);
        }
        finally
        {
            handle.Free();
        }
    }
}
