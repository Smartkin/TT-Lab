using System;
using System.Collections.Generic;
using Twinsanity.TwinsanityInterchange.Common;

namespace Twinsanity.Libraries;

/// <summary>
/// BC3/DXT5 block compression the Xbox version stores its textures with
/// </summary>
public static class Dxt5
{
    private const Int32 BlockLength = 16;

    /// <summary>
    /// Decodes the blocks into colors, row by row from the top
    /// </summary>
    public static List<Color> Decode(Byte[] data, Int32 width, Int32 height)
    {
        var colors = new Color[width * height];
        var blocksWide = Math.Max(1, (width + 3) / 4);
        var blocksHigh = Math.Max(1, (height + 3) / 4);
        var alphas = new Byte[8];
        var palette = new (Byte R, Byte G, Byte B)[4];
        for (var blockY = 0; blockY < blocksHigh; blockY++)
        {
            for (var blockX = 0; blockX < blocksWide; blockX++)
            {
                var offset = (blockY * blocksWide + blockX) * BlockLength;
                if (offset + BlockLength > data.Length)
                {
                    continue;
                }

                FillAlphas(data[offset], data[offset + 1], alphas);
                var color0 = BitConverter.ToUInt16(data, offset + 8);
                var color1 = BitConverter.ToUInt16(data, offset + 10);
                palette[0] = Expand565(color0);
                palette[1] = Expand565(color1);
                if (color0 > color1)
                {
                    palette[2] = Mix(palette[0], palette[1], 2, 1, 3);
                    palette[3] = Mix(palette[0], palette[1], 1, 2, 3);
                }
                else
                {
                    palette[2] = Mix(palette[0], palette[1], 1, 1, 2);
                    palette[3] = (0, 0, 0);
                }

                var alphaBits = 0UL;
                for (var i = 0; i < 6; i++)
                {
                    alphaBits |= (UInt64)data[offset + 2 + i] << (8 * i);
                }

                var colorBits = BitConverter.ToUInt32(data, offset + 12);
                for (var pixel = 0; pixel < 16; pixel++)
                {
                    var x = blockX * 4 + pixel % 4;
                    var y = blockY * 4 + pixel / 4;
                    if (x >= width || y >= height)
                    {
                        continue;
                    }

                    var color = palette[(colorBits >> (pixel * 2)) & 0x3];
                    var alpha = alphas[(alphaBits >> (pixel * 3)) & 0x7];
                    colors[y * width + x] = new Color(color.R, color.G, color.B, alpha);
                }
            }
        }

        return new List<Color>(colors);
    }

    /// <summary>
    /// Encodes the colors, given row by row from the top, fitting each block's colors and alphas between their extremes
    /// </summary>
    public static Byte[] Encode(IReadOnlyList<Color> colors, Int32 width, Int32 height)
    {
        var blocksWide = Math.Max(1, (width + 3) / 4);
        var blocksHigh = Math.Max(1, (height + 3) / 4);
        var result = new Byte[blocksWide * blocksHigh * BlockLength];
        var block = new Color[16];
        var alphas = new Byte[8];
        var palette = new (Byte R, Byte G, Byte B)[4];
        for (var blockY = 0; blockY < blocksHigh; blockY++)
        {
            for (var blockX = 0; blockX < blocksWide; blockX++)
            {
                for (var pixel = 0; pixel < 16; pixel++)
                {
                    // Pixels past the edge repeat the last ones so they don't pull the endpoints
                    var x = Math.Min(blockX * 4 + pixel % 4, width - 1);
                    var y = Math.Min(blockY * 4 + pixel / 4, height - 1);
                    block[pixel] = colors[y * width + x];
                }

                var offset = (blockY * blocksWide + blockX) * BlockLength;
                EncodeAlpha(block, alphas, result, offset);
                EncodeColor(block, palette, result, offset + 8);
            }
        }

        return result;
    }

    private static void EncodeAlpha(Color[] block, Byte[] alphas, Byte[] result, Int32 offset)
    {
        Byte min = 255;
        Byte max = 0;
        foreach (var color in block)
        {
            min = Math.Min(min, color.A);
            max = Math.Max(max, color.A);
        }

        // The first alpha being the larger one picks the mode with 6 values between them
        result[offset] = max;
        result[offset + 1] = min;
        FillAlphas(max, min, alphas);
        var bits = 0UL;
        for (var pixel = 0; pixel < 16; pixel++)
        {
            var best = 0;
            var bestDistance = Int32.MaxValue;
            for (var i = 0; i < 8; i++)
            {
                var distance = Math.Abs(alphas[i] - block[pixel].A);
                if (distance < bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }

            bits |= (UInt64)best << (pixel * 3);
        }

        for (var i = 0; i < 6; i++)
        {
            result[offset + 2 + i] = (Byte)(bits >> (8 * i));
        }
    }

    private static void EncodeColor(Color[] block, (Byte R, Byte G, Byte B)[] palette, Byte[] result, Int32 offset)
    {
        Byte minR = 255, minG = 255, minB = 255;
        Byte maxR = 0, maxG = 0, maxB = 0;
        foreach (var color in block)
        {
            minR = Math.Min(minR, color.R);
            minG = Math.Min(minG, color.G);
            minB = Math.Min(minB, color.B);
            maxR = Math.Max(maxR, color.R);
            maxG = Math.Max(maxG, color.G);
            maxB = Math.Max(maxB, color.B);
        }

        var color0 = To565(maxR, maxG, maxB);
        var color1 = To565(minR, minG, minB);
        // Equal endpoints would switch to the mode with a transparent black, every pixel is the first color then
        if (color0 == color1)
        {
            BitConverter.TryWriteBytes(result.AsSpan(offset), color0);
            BitConverter.TryWriteBytes(result.AsSpan(offset + 2), color1);
            BitConverter.TryWriteBytes(result.AsSpan(offset + 4), 0U);
            return;
        }

        if (color0 < color1)
        {
            (color0, color1) = (color1, color0);
        }

        palette[0] = Expand565(color0);
        palette[1] = Expand565(color1);
        palette[2] = Mix(palette[0], palette[1], 2, 1, 3);
        palette[3] = Mix(palette[0], palette[1], 1, 2, 3);
        var bits = 0U;
        for (var pixel = 0; pixel < 16; pixel++)
        {
            var best = 0;
            var bestDistance = Int32.MaxValue;
            for (var i = 0; i < 4; i++)
            {
                var dr = palette[i].R - block[pixel].R;
                var dg = palette[i].G - block[pixel].G;
                var db = palette[i].B - block[pixel].B;
                var distance = dr * dr + dg * dg + db * db;
                if (distance < bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }

            bits |= (UInt32)best << (pixel * 2);
        }

        BitConverter.TryWriteBytes(result.AsSpan(offset), color0);
        BitConverter.TryWriteBytes(result.AsSpan(offset + 2), color1);
        BitConverter.TryWriteBytes(result.AsSpan(offset + 4), bits);
    }

    private static void FillAlphas(Byte alpha0, Byte alpha1, Byte[] alphas)
    {
        alphas[0] = alpha0;
        alphas[1] = alpha1;
        if (alpha0 > alpha1)
        {
            for (var i = 1; i < 7; i++)
            {
                alphas[i + 1] = (Byte)(((7 - i) * alpha0 + i * alpha1) / 7);
            }
        }
        else
        {
            for (var i = 1; i < 5; i++)
            {
                alphas[i + 1] = (Byte)(((5 - i) * alpha0 + i * alpha1) / 5);
            }

            alphas[6] = 0;
            alphas[7] = 255;
        }
    }

    private static UInt16 To565(Byte r, Byte g, Byte b)
    {
        return (UInt16)((r * 31 + 127) / 255 << 11 | (g * 63 + 127) / 255 << 5 | (b * 31 + 127) / 255);
    }

    private static (Byte R, Byte G, Byte B) Expand565(UInt16 color)
    {
        var r = color >> 11 & 0x1F;
        var g = color >> 5 & 0x3F;
        var b = color & 0x1F;
        return ((Byte)(r << 3 | r >> 2), (Byte)(g << 2 | g >> 4), (Byte)(b << 3 | b >> 2));
    }

    private static (Byte R, Byte G, Byte B) Mix((Byte R, Byte G, Byte B) first, (Byte R, Byte G, Byte B) second, Int32 firstWeight, Int32 secondWeight, Int32 total)
    {
        return ((Byte)((first.R * firstWeight + second.R * secondWeight) / total),
            (Byte)((first.G * firstWeight + second.G * secondWeight) / total),
            (Byte)((first.B * firstWeight + second.B * secondWeight) / total));
    }
}
