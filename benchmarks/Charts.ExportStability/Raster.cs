using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Charts.ExportStability;

/// <summary>Premultiplied BGRA readback after decoding the actual encoded image.</summary>
internal sealed record Raster(int Width, int Height, byte[] Pixels)
{
    internal static Raster Read(Bitmap bitmap)
    {
        var size = bitmap.PixelSize;
        using var converted = new WriteableBitmap(size, new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var frame = converted.Lock(); bitmap.CopyPixels(frame);
        var pixels = new byte[checked(size.Width * size.Height * 4)];
        for (var y = 0; y < size.Height; y++)
            Marshal.Copy(frame.Address + y * frame.RowBytes, pixels, y * size.Width * 4, size.Width * 4);
        return new(size.Width, size.Height, pixels);
    }

    internal int CountBlue(Rect rectangle, int alphaMinimum)
    {
        var count = 0;
        for (var y = Math.Max(0, (int)Math.Ceiling(rectangle.Top)); y < Math.Min(Height, (int)Math.Floor(rectangle.Bottom)); y++)
            for (var x = Math.Max(0, (int)Math.Ceiling(rectangle.Left)); x < Math.Min(Width, (int)Math.Floor(rectangle.Right)); x++)
            {
                var offset = (y * Width + x) * 4; var alpha = Pixels[offset + 3];
                if (alpha < alphaMinimum) continue;
                if (Math.Abs(Pixels[offset] * 255d / alpha - 244) <= 24 &&
                    Math.Abs(Pixels[offset + 1] * 255d / alpha - 133) <= 24 &&
                    Math.Abs(Pixels[offset + 2] * 255d / alpha - 67) <= 24) count++;
            }
        return count;
    }
}
