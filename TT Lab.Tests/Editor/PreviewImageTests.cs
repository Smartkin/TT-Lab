using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using TT_Lab.Util;

namespace TT_Lab.Tests.Editor;

// A prefab's picture is the square of the scene around the selection, scaled to the tile's size
public sealed class PreviewImageTests
{
    private static WriteableBitmap Frame(int width, int height)
    {
        var frame = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
        using var buffer = frame.Lock();
        // Left half red, right half blue, opaque
        var row = new int[width];
        for (var x = 0; x < width; x++)
        {
            row[x] = unchecked((int)(x < width / 2 ? 0xFFFF0000 : 0xFF0000FF));
        }

        for (var y = 0; y < height; y++)
        {
            Marshal.Copy(row, 0, buffer.Address + y * buffer.RowBytes, width);
        }

        return frame;
    }

    private static uint PixelAt(Bitmap bitmap, int x, int y)
    {
        var pixels = new byte[4];
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            bitmap.CopyPixels(new PixelRect(x, y, 1, 1), handle.AddrOfPinnedObject(), 4, 4);
        }
        finally
        {
            handle.Free();
        }

        return BitConverter.ToUInt32(pixels, 0);
    }

    // A frame read back from GL has its rows from the bottom up, the picture is its middle square the right way up
    [AvaloniaFact]
    public void FramesReadBackFromGlAreTurnedTheRightWayUp()
    {
        const int width = 6;
        const int height = 4;
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                // The bottom row (GL's first) red, the rest blue
                var value = y == 0 ? 0xFFFF0000 : 0xFF0000FF;
                BitConverter.GetBytes(value).CopyTo(pixels, (y * width + x) * 4);
            }
        }

        using var picture = PreviewImage.FromPixels(pixels, width, height, 1, 0, 4, 4);

        Assert.Equal(new PixelSize(4, 4), picture.PixelSize);
        Assert.Equal(0xFF0000FF, PixelAt(picture, 1, 0));
        Assert.Equal(0xFFFF0000, PixelAt(picture, 1, 3));
    }

    [AvaloniaFact]
    public void TheSquareAroundThePointIsCutOutAndScaled()
    {
        using var frame = Frame(400, 200);

        // Half of the smaller side is 100 pixels: the square around the middle is red on its left and blue on its right
        using var middle = PreviewImage.Around(frame, new PixelPoint(200, 100), 40);
        Assert.Equal(new PixelSize(40, 40), middle.PixelSize);
        Assert.Equal(0xFFFF0000, PixelAt(middle, 5, 20));
        Assert.Equal(0xFF0000FF, PixelAt(middle, 35, 20));

        // Near the edge the square stays within the frame
        using var corner = PreviewImage.Around(frame, new PixelPoint(0, 0), 40);
        Assert.Equal(0xFFFF0000, PixelAt(corner, 35, 35));
        using var far = PreviewImage.Around(frame, new PixelPoint(399, 199), 40);
        Assert.Equal(0xFF0000FF, PixelAt(far, 5, 5));
    }
}
