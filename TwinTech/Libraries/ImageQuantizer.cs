using System;
using System.Collections.Generic;
using System.Linq;
using Twinsanity.TwinsanityInterchange.Common;

namespace Twinsanity.Libraries;

/// <summary>
/// Palettes for pictures with more colors than a palette holds, and the pictures drawn with them. Colors are told apart the way the eye
/// does, green the most and blue the least, alpha as much as green.
/// <para>The palette starts as a median cut of the picture's colors that splits the group with the most error where that error drops the
/// most, then k-means moves every entry to the average of the colors closest to it. Pictures get the closest entries with their color's
/// error diffused to the pixels after them (Floyd-Steinberg); alpha is matched as it is, so cut-outs keep their edges.</para>
/// </summary>
internal static class ImageQuantizer
{
    private const Int32 WeightR = 3;
    private const Int32 WeightG = 4;
    private const Int32 WeightB = 2;
    private const Int32 WeightA = 4;
    private const Int32 RefinementPasses = 8;
    // Less than all of the error goes on, all of it turns areas that are nearly flat into noise
    private const Single DitherStrength = 0.8f;

    private struct Entry
    {
        public Int32 R;
        public Int32 G;
        public Int32 B;
        public Int32 A;
        public Int32 Count;

        public readonly Int32 Channel(Int32 channel)
        {
            return channel switch
            {
                0 => R,
                1 => G,
                2 => B,
                _ => A
            };
        }
    }

    // The colors of order[Start..End) with their sums, the error is how far they are from their average
    private sealed class Box
    {
        public Int32 Start;
        public Int32 End;
        public Double Error;
    }

    private static readonly Int32[] Weights = { WeightR, WeightG, WeightB, WeightA };

    /// <summary>
    /// A palette of <paramref name="maxColors"/> entries for the picture
    /// </summary>
    public static List<Color> Quantize(IReadOnlyList<Color> image, Int32 maxColors = 256)
    {
        var entries = Histogram(image);
        var order = Enumerable.Range(0, entries.Length).ToArray();
        var boxes = new List<Box> { MakeBox(entries, order, 0, order.Length) };
        while (boxes.Count < maxColors)
        {
            Box worst = null;
            foreach (var box in boxes)
            {
                if (box.End - box.Start > 1 && box.Error > 0 && (worst == null || box.Error > worst.Error))
                {
                    worst = box;
                }
            }

            if (worst == null)
            {
                break;
            }

            var (first, second) = Split(entries, order, worst);
            boxes.Remove(worst);
            boxes.Add(first);
            boxes.Add(second);
        }

        var palette = boxes.Select(box => Average(entries, order, box.Start, box.End)).ToArray();
        Refine(entries, order, boxes, palette);
        var result = palette.Select(color => new Color((Byte)color[0], (Byte)color[1], (Byte)color[2], (Byte)color[3])).ToList();
        while (result.Count < maxColors)
        {
            // Never closer than the entry they copy, so never picked
            var last = result[result.Count - 1];
            result.Add(new Color(last.R, last.G, last.B, last.A));
        }

        return result;
    }

    /// <summary>
    /// The picture's palette indexes, the color's error of every pixel diffused to the pixels after it
    /// </summary>
    public static Byte[] Map(IReadOnlyList<Color> image, Int32 width, Int32 height, IReadOnlyList<Color> palette)
    {
        var entries = palette.Select(color => new[] { (Int32)color.R, color.G, color.B, color.A }).ToArray();
        var indexes = new Byte[width * height];
        // The errors waiting for this row and the next, with a pixel of room on both sides
        var current = new Single[(width + 2) * 3];
        var next = new Single[(width + 2) * 3];
        for (var y = 0; y < height; y++)
        {
            // Every other row goes back the other way, rows going one way only lean the errors that way
            var forward = (y & 1) == 0;
            var ahead = forward ? 1 : -1;
            for (var step = 0; step < width; step++)
            {
                var x = forward ? step : width - 1 - step;
                var pixel = image[y * width + x];
                var at = (x + 1) * 3;
                // What's see-through isn't seen, its color's error would only make the visible pixels around it noisy
                var visible = pixel.A > 0;
                var r = visible ? Math.Clamp(pixel.R + current[at], 0.0f, 255.0f) : pixel.R;
                var g = visible ? Math.Clamp(pixel.G + current[at + 1], 0.0f, 255.0f) : pixel.G;
                var b = visible ? Math.Clamp(pixel.B + current[at + 2], 0.0f, 255.0f) : pixel.B;
                var index = Nearest(entries, r, g, b, pixel.A);
                indexes[y * width + x] = (Byte)index;
                if (!visible)
                {
                    continue;
                }

                var chosen = entries[index];
                var errorR = (r - chosen[0]) * DitherStrength;
                var errorG = (g - chosen[1]) * DitherStrength;
                var errorB = (b - chosen[2]) * DitherStrength;
                Spread(current, x + 1 + ahead, errorR, errorG, errorB, 7.0f / 16.0f);
                Spread(next, x + 1 - ahead, errorR, errorG, errorB, 3.0f / 16.0f);
                Spread(next, x + 1, errorR, errorG, errorB, 5.0f / 16.0f);
                Spread(next, x + 1 + ahead, errorR, errorG, errorB, 1.0f / 16.0f);
            }

            (current, next) = (next, current);
            Array.Clear(next);
        }

        return indexes;
    }

    /// <summary>
    /// The picture half as big each way, every pixel the average of the four it covers. Colors count as much as their alpha, so what's
    /// see-through doesn't tint the pixels it's averaged with
    /// </summary>
    public static List<Color> HalfSize(IReadOnlyList<Color> image, Int32 width, Int32 height)
    {
        var halfWidth = Math.Max(width / 2, 1);
        var halfHeight = Math.Max(height / 2, 1);
        var result = new List<Color>(halfWidth * halfHeight);
        for (var y = 0; y < halfHeight; y++)
        {
            for (var x = 0; x < halfWidth; x++)
            {
                Int32 r = 0, g = 0, b = 0, a = 0, plainR = 0, plainG = 0, plainB = 0, count = 0;
                for (var dy = 0; dy < 2; dy++)
                {
                    for (var dx = 0; dx < 2; dx++)
                    {
                        var sourceX = Math.Min(x * 2 + dx, width - 1);
                        var sourceY = Math.Min(y * 2 + dy, height - 1);
                        var pixel = image[sourceY * width + sourceX];
                        r += pixel.R * pixel.A;
                        g += pixel.G * pixel.A;
                        b += pixel.B * pixel.A;
                        a += pixel.A;
                        plainR += pixel.R;
                        plainG += pixel.G;
                        plainB += pixel.B;
                        count++;
                    }
                }

                result.Add(a > 0
                    ? new Color((Byte)((r + a / 2) / a), (Byte)((g + a / 2) / a), (Byte)((b + a / 2) / a), (Byte)((a + count / 2) / count))
                    : new Color((Byte)((plainR + count / 2) / count), (Byte)((plainG + count / 2) / count), (Byte)((plainB + count / 2) / count), 0));
            }
        }

        return result;
    }

    private static void Spread(Single[] errors, Int32 column, Single r, Single g, Single b, Single share)
    {
        var at = column * 3;
        if (at < 0 || at + 2 >= errors.Length)
        {
            return;
        }

        errors[at] += r * share;
        errors[at + 1] += g * share;
        errors[at + 2] += b * share;
    }

    private static Int32 Nearest(Int32[][] palette, Single r, Single g, Single b, Single a)
    {
        var best = 0;
        var bestDistance = Single.MaxValue;
        for (var i = 0; i < palette.Length; i++)
        {
            var entry = palette[i];
            var dr = r - entry[0];
            var dg = g - entry[1];
            var db = b - entry[2];
            var da = a - entry[3];
            var distance = WeightR * dr * dr + WeightG * dg * dg + WeightB * db * db + WeightA * da * da;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }

        return best;
    }

    private static Entry[] Histogram(IReadOnlyList<Color> image)
    {
        var counts = new Dictionary<UInt32, Int32>();
        foreach (var color in image)
        {
            var key = color.ToARGB();
            counts.TryGetValue(key, out var count);
            counts[key] = count + 1;
        }

        return counts.Select(pair => new Entry
        {
            A = (Int32)(pair.Key >> 24),
            R = (Int32)(pair.Key >> 16 & 0xFF),
            G = (Int32)(pair.Key >> 8 & 0xFF),
            B = (Int32)(pair.Key & 0xFF),
            Count = pair.Value
        }).ToArray();
    }

    private static Box MakeBox(Entry[] entries, Int32[] order, Int32 start, Int32 end)
    {
        Double weight = 0;
        var sums = new Double[4];
        var squares = new Double[4];
        for (var i = start; i < end; i++)
        {
            var entry = entries[order[i]];
            weight += entry.Count;
            for (var channel = 0; channel < 4; channel++)
            {
                Double value = entry.Channel(channel);
                sums[channel] += value * entry.Count;
                squares[channel] += value * value * entry.Count;
            }
        }

        return new Box { Start = start, End = end, Error = Error(weight, sums, squares) };
    }

    private static Double Error(Double weight, Double[] sums, Double[] squares)
    {
        if (weight <= 0)
        {
            return 0;
        }

        Double error = 0;
        for (var channel = 0; channel < 4; channel++)
        {
            error += Weights[channel] * Math.Max(squares[channel] - sums[channel] * sums[channel] / weight, 0);
        }

        return error;
    }

    // Along the channel that has the most of the box's error, where the two halves together have the least
    private static (Box First, Box Second) Split(Entry[] entries, Int32[] order, Box box)
    {
        var count = box.End - box.Start;
        var channel = WidestChannel(entries, order, box);
        var keys = new Int32[count];
        var items = new Int32[count];
        for (var i = 0; i < count; i++)
        {
            items[i] = order[box.Start + i];
            keys[i] = entries[items[i]].Channel(channel);
        }

        Array.Sort(keys, items);
        Array.Copy(items, 0, order, box.Start, count);

        // Sums of the colors before every place
        var weights = new Double[count + 1];
        var sums = new Double[4, count + 1];
        var squares = new Double[4, count + 1];
        for (var i = 0; i < count; i++)
        {
            var entry = entries[items[i]];
            weights[i + 1] = weights[i] + entry.Count;
            for (var c = 0; c < 4; c++)
            {
                Double value = entry.Channel(c);
                sums[c, i + 1] = sums[c, i] + value * entry.Count;
                squares[c, i + 1] = squares[c, i] + value * value * entry.Count;
            }
        }

        var bestAt = count / 2;
        var bestError = Double.MaxValue;
        var left = new Double[4];
        var leftSquares = new Double[4];
        var right = new Double[4];
        var rightSquares = new Double[4];
        for (var at = 1; at < count; at++)
        {
            // Colors of one value stay together
            if (keys[at] == keys[at - 1])
            {
                continue;
            }

            for (var c = 0; c < 4; c++)
            {
                left[c] = sums[c, at];
                leftSquares[c] = squares[c, at];
                right[c] = sums[c, count] - sums[c, at];
                rightSquares[c] = squares[c, count] - squares[c, at];
            }

            var error = Error(weights[at], left, leftSquares) + Error(weights[count] - weights[at], right, rightSquares);
            if (error < bestError)
            {
                bestError = error;
                bestAt = at;
            }
        }

        return (MakeBox(entries, order, box.Start, box.Start + bestAt), MakeBox(entries, order, box.Start + bestAt, box.End));
    }

    private static Int32 WidestChannel(Entry[] entries, Int32[] order, Box box)
    {
        Double weight = 0;
        var sums = new Double[4];
        var squares = new Double[4];
        for (var i = box.Start; i < box.End; i++)
        {
            var entry = entries[order[i]];
            weight += entry.Count;
            for (var channel = 0; channel < 4; channel++)
            {
                Double value = entry.Channel(channel);
                sums[channel] += value * entry.Count;
                squares[channel] += value * value * entry.Count;
            }
        }

        var widest = 0;
        Double widestError = -1;
        for (var channel = 0; channel < 4; channel++)
        {
            var error = Weights[channel] * (squares[channel] - sums[channel] * sums[channel] / weight);
            if (error > widestError)
            {
                widestError = error;
                widest = channel;
            }
        }

        return widest;
    }

    private static Int32[] Average(Entry[] entries, Int32[] order, Int32 start, Int32 end)
    {
        Double weight = 0;
        var sums = new Double[4];
        for (var i = start; i < end; i++)
        {
            var entry = entries[order[i]];
            weight += entry.Count;
            for (var channel = 0; channel < 4; channel++)
            {
                sums[channel] += (Double)entry.Channel(channel) * entry.Count;
            }
        }

        return sums.Select(sum => (Int32)Math.Clamp(Math.Round(sum / weight), 0, 255)).ToArray();
    }

    // k-means: every entry moves to the average of the colors closest to it, until they stop moving
    private static void Refine(Entry[] entries, Int32[] order, List<Box> boxes, Int32[][] palette)
    {
        var assignment = new Int32[entries.Length];
        for (var box = 0; box < boxes.Count; box++)
        {
            for (var i = boxes[box].Start; i < boxes[box].End; i++)
            {
                assignment[order[i]] = box;
            }
        }

        for (var pass = 0; pass < RefinementPasses; pass++)
        {
            var weights = new Double[palette.Length];
            var sums = new Double[palette.Length, 4];
            var moved = 0;
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                var nearest = Nearest(palette, entry.R, entry.G, entry.B, entry.A);
                if (nearest != assignment[i])
                {
                    moved++;
                    assignment[i] = nearest;
                }

                weights[nearest] += entry.Count;
                for (var channel = 0; channel < 4; channel++)
                {
                    sums[nearest, channel] += (Double)entry.Channel(channel) * entry.Count;
                }
            }

            for (var i = 0; i < palette.Length; i++)
            {
                if (weights[i] <= 0)
                {
                    continue;
                }

                for (var channel = 0; channel < 4; channel++)
                {
                    palette[i][channel] = (Int32)Math.Clamp(Math.Round(sums[i, channel] / weights[i]), 0, 255);
                }
            }

            if (moved == 0 && pass > 0)
            {
                break;
            }
        }
    }
}
