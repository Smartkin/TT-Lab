using System;
using System.Collections.Generic;
using System.Linq;

namespace TT_Lab.AssetData.Global;

/// <summary>
/// Where a part of a PSM is in the picture the editor shows
/// </summary>
public sealed record PsmPartPlace(int Index, int X, int Y, int Width, int Height);

/// <summary>
/// How a PSM's parts make up the picture shown. The game's pictures (the legal, loading, game over and credits screens, the gallery) are
/// 8 tiles of 128x256 laid 4 across from the top left, the level titles one tile. Icons.psm is 40 icons of several sizes the HUD draws one
/// at a time, so they're laid out on a sheet. Every part is kept upside down, like the fonts' pages
/// </summary>
public sealed class PsmLayout
{
    public const int PictureColumns = 4;
    public const int SheetColumns = 8;
    public const int SheetGap = 8;

    private PsmLayout(bool isPicture, int width, int height, IReadOnlyList<PsmPartPlace> parts)
    {
        IsPicture = isPicture;
        Width = width;
        Height = height;
        Parts = parts;
    }

    /// <summary>
    /// Whether the parts are tiles of one picture, which gets replaced whole, rather than pictures of their own
    /// </summary>
    public bool IsPicture { get; }

    public int Width { get; }

    public int Height { get; }

    public IReadOnlyList<PsmPartPlace> Parts { get; }

    /// <summary>
    /// The game's icons are pictures of their own whatever their sizes, the other PSMs' parts are tiles when they're all the same size
    /// </summary>
    public static bool AreTiles(string name, IReadOnlyList<(int Width, int Height)> sizes)
    {
        return !string.Equals(name, "Icons", StringComparison.OrdinalIgnoreCase) && sizes.Count > 0 && sizes.All(size => size == sizes[0])
               && (sizes.Count == 1 || sizes.Count % PictureColumns == 0);
    }

    public static PsmLayout Of(string name, IReadOnlyList<(int Width, int Height)> sizes)
    {
        if (sizes.Count == 0)
        {
            return new PsmLayout(false, 0, 0, []);
        }

        if (AreTiles(name, sizes))
        {
            var (tileWidth, tileHeight) = sizes[0];
            var columns = Math.Min(PictureColumns, sizes.Count);
            var tiles = sizes.Select((_, index) => new PsmPartPlace(index, index % columns * tileWidth, index / columns * tileHeight, tileWidth, tileHeight)).ToList();
            return new PsmLayout(true, columns * tileWidth, (sizes.Count + columns - 1) / columns * tileHeight, tiles);
        }

        // Each in the middle of a cell the size of the biggest
        var cellWidth = sizes.Max(size => size.Width);
        var cellHeight = sizes.Max(size => size.Height);
        var sheetColumns = Math.Min(SheetColumns, sizes.Count);
        var rows = (sizes.Count + sheetColumns - 1) / sheetColumns;
        var places = sizes.Select((size, index) => new PsmPartPlace(index,
            index % sheetColumns * (cellWidth + SheetGap) + (cellWidth - size.Width) / 2,
            index / sheetColumns * (cellHeight + SheetGap) + (cellHeight - size.Height) / 2,
            size.Width, size.Height)).ToList();
        return new PsmLayout(false, sheetColumns * (cellWidth + SheetGap) - SheetGap, rows * (cellHeight + SheetGap) - SheetGap, places);
    }

    /// <summary>
    /// The picture (ARGB, top row first) of the parts' pixels as the game keeps them: each turned the right way up in its place, what's
    /// between the icons transparent. A part without pixels stays transparent
    /// </summary>
    public UInt32[] Compose(IReadOnlyList<UInt32[]?> parts)
    {
        var picture = new UInt32[Width * Height];
        foreach (var place in Parts)
        {
            if (place.Index >= parts.Count || parts[place.Index] is not { } pixels || pixels.Length != place.Width * place.Height)
            {
                continue;
            }

            for (var row = 0; row < place.Height; row++)
            {
                Array.Copy(pixels, (place.Height - 1 - row) * place.Width, picture, (place.Y + row) * Width + place.X, place.Width);
            }
        }

        return picture;
    }

    /// <summary>
    /// A part's pixels the way the game keeps them (upside down) out of a picture of the layout's size
    /// </summary>
    public UInt32[] Cut(UInt32[] picture, int index)
    {
        var place = Parts[index];
        var pixels = new UInt32[place.Width * place.Height];
        for (var row = 0; row < place.Height; row++)
        {
            Array.Copy(picture, (place.Y + row) * Width + place.X, pixels, (place.Height - 1 - row) * place.Width, place.Width);
        }

        return pixels;
    }

    /// <summary>
    /// The rows the other way round: a part shown the right way up to the game's way and back
    /// </summary>
    public static UInt32[] Flip(UInt32[] pixels, int width, int height)
    {
        var flipped = new UInt32[pixels.Length];
        for (var row = 0; row < height; row++)
        {
            Array.Copy(pixels, row * width, flipped, (height - 1 - row) * width, width);
        }

        return flipped;
    }
}
