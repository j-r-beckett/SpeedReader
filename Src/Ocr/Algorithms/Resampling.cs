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
    /// <exception cref="ArgumentException">Thrown when destination buffer size doesn't match expected dimensions</exception>
    /// <exception cref="NonContiguousImageException">Thrown when ImageSharp fails to provide contiguous pixel memory</exception>
    /// <remarks>
    /// Uses bicubic resampling. The scaled image fills maximum area while preserving aspect ratio.
    /// Used by DBNet preprocessing.
    /// </remarks>
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

        ResizeToExactDimensions(src, targetWidth, targetHeight, out Memory<Rgb24> resizedMemory);

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
    /// Scales image to exact target height, calculates proportional width with min/max constraints, then left-aligns
    /// with black padding on right.
    /// </summary>
    /// <param name="src">Source image to resize</param>
    /// <param name="dest">Destination buffer for RGB pixel data in HWC layout</param>
    /// <param name="destWidth">Target width (used for padding bounds)</param>
    /// <param name="destHeight">Target height (fixed dimension)</param>
    /// <param name="minWidth">Minimum allowed width after scaling</param>
    /// <param name="maxWidth">Maximum allowed width after scaling</param>
    /// <exception cref="ArgumentException">Thrown when destination buffer size doesn't match expected dimensions</exception>
    /// <exception cref="NonContiguousImageException">Thrown when ImageSharp fails to provide contiguous pixel memory</exception>
    /// <remarks>
    /// Uses bicubic resampling. Height is fixed, width varies with aspect ratio constraints.
    /// Used by SVTRv2 preprocessing for variable-width text recognition.
    /// </remarks>
    // public static void ScaleResizeInto(Image<Rgb24> src, Span<float> dest, int destWidth, int destHeight, int minWidth, int maxWidth)
    // {
    //     if (destWidth * destHeight * 3 != dest.Length)
    //     {
    //         throw new ArgumentException(
    //             $"Expected buffer size {destWidth * destHeight * 3}, actual size was {dest.Length}");
    //     }
    //
    //     // Calculate target width maintaining aspect ratio
    //     double aspectRatio = (double)src.Width / src.Height;
    //     int targetWidth = (int)Math.Round(aspectRatio * destHeight);
    //     targetWidth = Math.Max(minWidth, Math.Min(destWidth, targetWidth));
    //
    //     ResizeToExactDimensions(src, targetWidth, destHeight, out Memory<Rgb24> resizedMemory);
    //
    //     // Clear destination buffer to black (padding)
    //     dest.Clear();
    //
    //     // Copy resized image to the left side of destination buffer in HWC format
    //     for (int y = 0; y < destHeight; y++)
    //     {
    //         for (int x = 0; x < targetWidth; x++)
    //         {
    //             var pixel = resizedMemory.Span[y * targetWidth + x];
    //             int destIndex = (y * destWidth + x) * 3;
    //             dest[destIndex] = pixel.R;      // R
    //             dest[destIndex + 1] = pixel.G;  // G
    //             dest[destIndex + 2] = pixel.B;  // B
    //         }
    //     }
    // }

    /// <summary>
    /// Crops a region from the source image, scales to specified dimensions, then left-aligns with black padding on right.
    /// </summary>
    /// <param name="src">Source image to crop from</param>
    /// <param name="cropRect">Rectangle defining the region to crop</param>
    /// <param name="dest">Destination buffer for RGB pixel data in HWC layout</param>
    /// <param name="destWidth">Buffer width (used for padding bounds)</param>
    /// <param name="destHeight">Target height after resize</param>
    /// <param name="targetWidth">Target width after resize (pre-calculated to maintain aspect ratio)</param>
    /// <exception cref="ArgumentException">Thrown when destination buffer size doesn't match expected dimensions</exception>
    /// <exception cref="NonContiguousImageException">Thrown when ImageSharp fails to provide contiguous pixel memory</exception>
    /// <remarks>
    /// Uses bicubic resampling. Caller should pre-calculate targetWidth based on aspect ratio constraints.
    /// Temporary implementation using ImageSharp crop - can be optimized to true zero-copy in future.
    /// Used by SVTRv2 preprocessing for cropped text region recognition.
    /// </remarks>
    public static void CropResizeInto(Image<Rgb24> src, Rectangle cropRect, Span<float> dest, int destWidth, int destHeight, int targetWidth)
    {
        if (destWidth * destHeight * 3 != dest.Length)
        {
            throw new ArgumentException(
                $"Expected buffer size {destWidth * destHeight * 3}, actual size was {dest.Length}");
        }

        CropAndResizeToExactDimensions(src, cropRect, targetWidth, destHeight, out Memory<Rgb24> resizedMemory);

        // Clear destination buffer to black (padding)
        dest.Clear();

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

    /// <summary>
    /// Crops a region from the image and resizes to exact dimensions using bicubic resampling.
    /// </summary>
    /// <param name="src">Source image to crop and resize</param>
    /// <param name="cropRect">Rectangle defining the region to crop</param>
    /// <param name="width">Target width</param>
    /// <param name="height">Target height</param>
    /// <param name="resizedMemory">Output contiguous pixel memory from cropped and resized image</param>
    /// <exception cref="NonContiguousImageException">Thrown when ImageSharp fails to provide contiguous pixel memory</exception>
    private static void CropAndResizeToExactDimensions(Image<Rgb24> src, Rectangle cropRect, int width, int height, out Memory<Rgb24> resizedMemory)
    {
        var config = Configuration.Default.Clone();
        config.PreferContiguousImageBuffers = true;
        using var cropped = src.Clone(config, x => x
            .Crop(cropRect)
            .Resize(new ResizeOptions
            {
                Size = new Size(width, height),
                Mode = ResizeMode.Stretch,
                Sampler = KnownResamplers.Bicubic
            }));

        if (!cropped.DangerousTryGetSinglePixelMemory(out Memory<Rgb24> tempMemory))
        {
            throw new NonContiguousImageException("Image memory is not contiguous after crop and resize operations");
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
