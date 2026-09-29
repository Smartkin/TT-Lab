using System;
using System.Collections.Generic;
using System.Linq;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.AssetData.Instance.Particle;

// A particle's picture: a rectangle of one of the default chunk's texture pages, in the page's pixels from its top left corner, and
// whether it's drawn mirrored. The game stores the start and end corners plus 2^19 (2^18 in a few systems), adds another 2^19 and
// keeps the low 10 bits of the pixel; a start after the end mirrors the picture along that axis
public readonly record struct ParticleTextureRect(int X, int Y, int Width, int Height, bool MirrorX = false, bool MirrorY = false)
{
    public const Single GameOffset = 1 << 19;
    public const int MaxPixel = 0x3FF;

    public int Right => X + Width;
    public int Bottom => Y + Height;

    public static int Pixel(Single value) => (int)(value + GameOffset) & MaxPixel;

    public static ParticleTextureRect FromGame(Vector2 start, Vector2 end)
    {
        var (startX, startY, endX, endY) = (Pixel(start.X), Pixel(start.Y), Pixel(end.X), Pixel(end.Y));
        return new ParticleTextureRect(Math.Min(startX, endX), Math.Min(startY, endY), Math.Abs(endX - startX), Math.Abs(endY - startY), endX < startX, endY < startY);
    }

    // The start and end corners the game stores for the rectangle, made from the ones it had: a value keeps its offset and fraction
    // while its pixel stays and moves by as many pixels as its pixel does
    public (Vector2 Start, Vector2 End) ToGame(Vector2 start, Vector2 end)
    {
        var (startX, endX) = MirrorX ? (Right, X) : (X, Right);
        var (startY, endY) = MirrorY ? (Bottom, Y) : (Y, Bottom);
        return (new Vector2 { X = Keep(start.X, startX), Y = Keep(start.Y, startY) }, new Vector2 { X = Keep(end.X, endX), Y = Keep(end.Y, endY) });
    }

    private static Single Keep(Single value, int pixel)
    {
        var current = Pixel(value);
        if (current == pixel)
        {
            return value;
        }

        // Values the tools didn't write, like a new system's zeros, get the tools' offset
        return Math.Abs(value) >= 1 << 18 ? value + (pixel - current) : pixel + GameOffset;
    }

    // The rectangle within a page of the size, at least a pixel wide and high
    public ParticleTextureRect Within(int pageWidth, int pageHeight)
    {
        var width = Math.Clamp(Width, 1, Math.Max(1, pageWidth));
        var height = Math.Clamp(Height, 1, Math.Max(1, pageHeight));
        return this with { X = Math.Clamp(X, 0, Math.Max(0, pageWidth - width)), Y = Math.Clamp(Y, 0, Math.Max(0, pageHeight - height)), Width = width, Height = height };
    }

    public bool Contains(double x, double y) => x >= X && x < Right && y >= Y && y < Bottom;
}

// A picture the game's particle systems cut out of a page, drawn with the rectangle most of them use
public sealed record ParticleSprite(int Page, ParticleTextureRect Rect, IReadOnlyList<string> UsedBy)
{
    public string Description => UsedBy.Count <= 3 ? $"Used by {string.Join(", ", UsedBy)}" : $"Used by {string.Join(", ", UsedBy.Take(3))} and {UsedBy.Count - 3} more";
}

public static class ParticleTextureBank
{
    // Rectangles this close on every side are the same picture, the systems' rectangles around a picture differ by a pixel or two
    public const int Tolerance = 3;

    public static List<ParticleSprite> Build(IEnumerable<ParticleSystem> systems)
    {
        var clusters = new List<(int Page, List<(ParticleTextureRect Rect, string Name)> Members)>();
        foreach (var system in systems)
        {
            var rect = ParticleTextureRect.FromGame(system.TextureStart, system.TextureEnd) with { MirrorX = false, MirrorY = false };
            if (rect.Width == 0 || rect.Height == 0)
            {
                continue;
            }

            var cluster = clusters.FirstOrDefault(cluster => cluster.Page == system.TexturePage && cluster.Members.Any(member => IsNear(member.Rect, rect)));
            if (cluster.Members == null)
            {
                clusters.Add((system.TexturePage, [(rect, system.Name)]));
            }
            else
            {
                cluster.Members.Add((rect, system.Name));
            }
        }

        return clusters.Select(cluster => new ParticleSprite(cluster.Page,
                cluster.Members.GroupBy(member => member.Rect).OrderByDescending(group => group.Count()).ThenBy(group => group.Key.Y).ThenBy(group => group.Key.X).First().Key,
                cluster.Members.Select(member => member.Name).Distinct().ToList()))
            .OrderBy(sprite => sprite.Page).ThenBy(sprite => sprite.Rect.Y).ThenBy(sprite => sprite.Rect.X)
            .ToList();
    }

    private static bool IsNear(ParticleTextureRect a, ParticleTextureRect b)
    {
        return Math.Abs(a.X - b.X) <= Tolerance && Math.Abs(a.Y - b.Y) <= Tolerance && Math.Abs(a.Right - b.Right) <= Tolerance && Math.Abs(a.Bottom - b.Bottom) <= Tolerance;
    }
}
