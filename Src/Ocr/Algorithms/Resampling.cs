using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Ocr.Algorithms;

public static class Resampling
{
    /// <summary>
    /// Scales image as large as possible (preserves aspect ratio) while fitting within destination bounds, then places
    /// at top-left corner of dest.
    /// </summary>
    /// <param name="src">Source image to resize</param>
    /// <param name="dest">Destination buffer for RGB pixel data in HWC layout</param>
    /// <param name="destWidth">Target width</param>
    /// <param name="destHeight">Target height</param>
    /// <param name="fillColor">Fill color for padding areas (default: black)</param>
    /// <exception cref="ArgumentException">Thrown when destination buffer size doesn't match expected dimensions</exception>
    /// <exception cref="NonContiguousImageException">Thrown when ImageSharp fails to provide contiguous pixel memory</exception>
    /// <remarks>
    /// Uses bicubic resampling. The scaled image fills maximum area while preserving aspect ratio.
    /// Used by DBNet preprocessing.
    /// </remarks>
    public static void AspectResizeInto(Image<Rgb24> src, Span<float> dest, int destWidth, int destHeight, float fillColor = 0.0f)
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

        ResizeToExactDimensions(src, targetWidth, targetHeight, out Memory<Rgb24> resizedMemory);

        // Clear destination to specified fill color (padding)
        dest.Fill(fillColor);

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
    /// Resizes image to exact dimensions using bicubic resampling.
    /// </summary>
    /// <param name="src">Source image to resize</param>
    /// <param name="width">Target width</param>
    /// <param name="height">Target height</param>
    /// <param name="resizedMemory">Output contiguous pixel memory from resized image</param>
    /// <exception cref="NonContiguousImageException">Thrown when ImageSharp fails to provide contiguous pixel memory</exception>
    private static void ResizeToExactDimensions(Image<Rgb24> src, int width, int height, out Memory<Rgb24> resizedMemory)
    {
        var config = Configuration.Default.Clone();
        config.PreferContiguousImageBuffers = true;
        using var resized = src.Clone(config, x => x
            .Resize(new ResizeOptions
            {
                Size = new Size(width, height),
                Mode = ResizeMode.Stretch,
                Sampler = KnownResamplers.Bicubic
            }));

        if (!resized.DangerousTryGetSinglePixelMemory(out Memory<Rgb24> tempMemory))
        {
            throw new NonContiguousImageException("Image memory is not contiguous after resize operations");
        }

        // Create independent copy to avoid ImageSharp lifetime issues
        var pixelData = new Rgb24[width * height];
        tempMemory.Span.CopyTo(pixelData);
        resizedMemory = pixelData;
    }

}

public class NonContiguousImageException : InvalidOperationException
{
    public NonContiguousImageException(string message) : base(message) { }
}
