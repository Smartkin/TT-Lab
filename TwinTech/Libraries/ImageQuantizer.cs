using System;
using System.Collections.Generic;
using System.Linq;
using Twinsanity.TwinsanityInterchange.Common;

namespace Twinsanity.Libraries;

internal static class ImageQuantizer
{
    private class ColorBox
    {
        private List<Color> _colors;

        public ColorBox(List<Color> colors)
        {
            _colors = colors;
        }

        public int RangeA => _colors.Max(c => c.A) - _colors.Min(c => c.A);
        public int RangeR => _colors.Max(c => c.R) - _colors.Min(c => c.R);
        public int RangeG => _colors.Max(c => c.G) - _colors.Min(c => c.G);
        public int RangeB => _colors.Max(c => c.B) - _colors.Min(c => c.B);
        public int ColorsStored => _colors.Count;

        public void Split(out ColorBox box1, out ColorBox box2)
        {
            if (RangeR >= RangeG && RangeR >= RangeB && RangeR >= RangeA)
            {
                _colors = _colors.OrderBy(c => c.R).ToList();
            }
            else if (RangeG >= RangeB && RangeG >= RangeA)
            {
                _colors = _colors.OrderBy(c => c.G).ToList();
            }
            else if (RangeB >= RangeA)
            {
                _colors = _colors.OrderBy(c => c.B).ToList();
            }
            else
            {
                _colors = _colors.OrderBy(c => c.A).ToList();
            }

            var mid = _colors.Count / 2;
            box1 = new ColorBox(_colors.Take(mid).ToList());
            box2 = new ColorBox(_colors.Skip(mid).ToList());
        }

        public Color AverageColor()
        {
            var r = 0;
            var g = 0;
            var b = 0;
            var a = 0;
            foreach (var color in _colors)
            {
                r += color.R;
                g += color.G;
                b += color.B;
                a += color.A;
            }
            
            return new Color((byte)(r / _colors.Count), (byte)(g / _colors.Count), (byte)(b / _colors.Count), (byte)(a / _colors.Count));
        }
    }

    public static List<Color> Quantize(List<Color> image, int maxColors = 256)
    {
        var boxes = new List<ColorBox> { new(image) };

        while (boxes.Count < maxColors)
        {
            var boxToSplit =
                boxes.OrderByDescending(b => Math.Max(b.RangeR, Math.Max(b.RangeG, Math.Max(b.RangeB, b.RangeA)))).First();

            boxToSplit.Split(out var box1, out var box2);
            if (box1.ColorsStored == 0 || box2.ColorsStored == 0)
            {
                break;
            }
            
            boxes.Remove(boxToSplit);
            
            boxes.Add(box1);
            boxes.Add(box2);
        }

        var palette = boxes.Select(b => b.AverageColor()).ToList();
        while (palette.Count < maxColors)
        {
            palette.Add(new Color());
        }

        return palette;
    }

    public static Byte PaletteIndex(Color pixel, List<Color> palette)
    {
        var minDist = Double.MaxValue;
        var resultIndex = 0;
        var paletteIndex = 0;
        foreach (var color in palette)
        {
            var pixelDist = Math.Sqrt((color.R - pixel.R)*(color.R - pixel.R) + (color.G - pixel.G)*(color.G - pixel.G) + (color.B - pixel.B)*(color.B - pixel.B) + (color.A - pixel.A)*(color.A - pixel.A));
            if (pixelDist < minDist)
            {
                minDist = pixelDist;
                resultIndex = paletteIndex;
            }
            
            paletteIndex++;
        }
        
        return (Byte)resultIndex;
    }
}