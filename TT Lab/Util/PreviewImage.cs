using System;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace TT_Lab.Util;

/// <summary>
/// Small pictures cut out of a viewport's frame, for the prefabs
/// </summary>
public static class PreviewImage
{
    public const int Size = 160;

    /// <summary>
    /// The square of the frame around the point, half of the frame's smaller side wide, scaled to the preview size
    /// </summary>
    public static Bitmap Around(Bitmap frame, PixelPoint center, int size = Size)
    {
        var frameSize = frame.PixelSize;
        var side = Math.Max(8, Math.Min(frameSize.Width, frameSize.Height) / 2);
        var left = Math.Clamp(center.X - side / 2, 0, Math.Max(0, frameSize.Width - side));
        var top = Math.Clamp(center.Y - side / 2, 0, Math.Max(0, frameSize.Height - side));
        return Crop(frame, new PixelRect(left, top, Math.Min(side, frameSize.Width), Math.Min(side, frameSize.Height)), size);
    }

    /// <summary>
    /// A square of a frame read back from GL (BGRA, rows from the bottom up), scaled to the preview size
    /// </summary>
    public static Bitmap FromPixels(byte[] bottomUpBgra, int width, int height, int left, int top, int side, int size)
    {
        using var square = new WriteableBitmap(new PixelSize(side, side), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        using (var buffer = square.Lock())
        {
            for (var y = 0; y < side; y++)
            {
                var sourceRow = height - 1 - (top + y);
                Marshal.Copy(bottomUpBgra, (sourceRow * width + left) * 4, buffer.Address + y * buffer.RowBytes, side * 4);
            }
        }

        return Scale(square, size);
    }

    public static Bitmap Crop(Bitmap frame, PixelRect rect, int size)
    {
        var stride = rect.Width * 4;
        var pixels = new byte[stride * rect.Height];
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            frame.CopyPixels(rect, handle.AddrOfPinnedObject(), pixels.Length, stride);
            using var cropped = new WriteableBitmap(rect.Size, new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
            using (var buffer = cropped.Lock())
            {
                for (var y = 0; y < rect.Height; y++)
                {
                    Marshal.Copy(pixels, y * stride, buffer.Address + y * buffer.RowBytes, stride);
                }
            }

            return Scale(cropped, size);
        }
        finally
        {
            handle.Free();
        }
    }

    // Skia can't scale a writeable bitmap in place, decoding the PNG of it at the size does
    private static Bitmap Scale(WriteableBitmap bitmap, int size)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream);
        // Avalonia encodes a snapshot of the bitmap's pixels without holding on to the bitmap (WriteableBitmapImpl.Save): one nothing
        // referenced any more got finalized in the middle of it, its pixels freed, and Release builds crashed in Skia
        GC.KeepAlive(bitmap);
        stream.Position = 0;
        return Bitmap.DecodeToWidth(stream, size);
    }
}
