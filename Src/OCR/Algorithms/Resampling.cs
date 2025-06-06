using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace OCR.Algorithms;

public static class Resampling
{
    public static void AspectResizeInto(Image<Rgb24> src, Span<float> dest, int destWidth, int destHeight)
    {
        if (destWidth * destHeight * 3 != dest.Length)
        {
            throw new ArgumentException(
                $"Expected buffer size {destWidth * destHeight}, actual size was {dest.Length}");
        }

        // Calculate dimensions preserving aspect ratio
        double scale = Math.Min((double)destWidth / src.Width, (double)destHeight / src.Height);
        int targetWidth = (int)Math.Round(src.Width * scale);
        int targetHeight = (int)Math.Round(src.Height * scale);

        var config = Configuration.Default.Clone();
        config.PreferContiguousImageBuffers = true;
        using var resized = src.Clone(config, x => x
            .Resize(new ResizeOptions
            {
                Size = new Size(targetWidth, targetHeight),
                Mode = ResizeMode.Stretch,
                Sampler = KnownResamplers.Bicubic
            }));

        if (!resized.DangerousTryGetSinglePixelMemory(out Memory<Rgb24> resizedMemory))
        {
            throw new NonContiguousImageException("Image memory is not contiguous after resize operations");
        }

        // Clear destination to black (padding)
        dest.Fill(0.0f);

        // Copy resized image to top-left of destination buffer in HWC format
        for (int y = 0; y < targetHeight; y++)
        {
            for (int x = 0; x < targetWidth; x++)
            {
                var pixel = resizedMemory.Span[y * targetWidth + x];
                int destIndex = (y * destWidth + x) * 3;
                dest[destIndex] = pixel.R;      // R
                dest[destIndex + 1] = pixel.G;  // G
                dest[destIndex + 2] = pixel.B;  // B
            }
        }
    }

    /// <summary>
    /// Scales image with aspect ratio preservation and manual padding (SVTRv2 approach).
    /// Scales to target height, maintains aspect ratio, then pads on the right with black.
    /// </summary>
    public static void ScaleResizeInto(Image<Rgb24> src, Span<float> dest, int destWidth, int destHeight, int minWidth, int maxWidth)
    {
        if (destWidth * destHeight * 3 != dest.Length)
        {
            throw new ArgumentException(
                $"Expected buffer size {destWidth * destHeight * 3}, actual size was {dest.Length}");
        }

        // Calculate target width maintaining aspect ratio
        double aspectRatio = (double)src.Width / src.Height;
        int targetWidth = (int)Math.Round(aspectRatio * destHeight);
        targetWidth = Math.Max(minWidth, Math.Min(destWidth, targetWidth));

        var config = Configuration.Default.Clone();
        config.PreferContiguousImageBuffers = true;
        using var resized = src.Clone(config, x => x
            .Resize(new ResizeOptions
            {
                Size = new Size(targetWidth, destHeight),
                Mode = ResizeMode.Stretch,
                Sampler = KnownResamplers.Bicubic
            }));

        if (!resized.DangerousTryGetSinglePixelMemory(out Memory<Rgb24> resizedMemory))
        {
            throw new NonContiguousImageException("Image memory is not contiguous after resize operations");
        }

        // Clear destination buffer to black (padding)
        dest.Fill(0.0f);

        // Copy resized image to the left side of destination buffer in HWC format
        for (int y = 0; y < destHeight; y++)
        {
            for (int x = 0; x < targetWidth; x++)
            {
                var pixel = resizedMemory.Span[y * targetWidth + x];
                int destIndex = (y * destWidth + x) * 3;
                dest[destIndex] = pixel.R;      // R
                dest[destIndex + 1] = pixel.G;  // G  
                dest[destIndex + 2] = pixel.B;  // B
            }
        }
    }

}

public class NonContiguousImageException : InvalidOperationException
{
    public NonContiguousImageException(string message) : base(message) { }
}
