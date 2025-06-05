using System.Numerics.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace OCR.Algorithms;

public static class Resize
{
    /// <summary>
    /// Resizes image to exact dimensions with padding (DBNet approach).
    /// Scales image to fit within bounds and pads with black to reach exact dimensions.
    /// </summary>
    public static void AspectResizeInto(Image<Rgb24> src, Memory<Rgb24> dest, int destWidth, int destHeight)
    {
        if (destWidth * destHeight != dest.Length)
        {
            throw new ArgumentException(
                $"Expected buffer size {destWidth * destHeight}, actual size was {dest.Length}");
        }

        var config = Configuration.Default.Clone();
        config.PreferContiguousImageBuffers = true;
        using var resized = src.Clone(config, x => x
            .Resize(new ResizeOptions
            {
                Size = new Size(destWidth, destHeight),
                Mode = ResizeMode.Pad,
                Position = AnchorPositionMode.TopLeft,
                PadColor = Color.Black,
                Sampler = KnownResamplers.Bicubic
            }));

        if (!resized.DangerousTryGetSinglePixelMemory(out Memory<Rgb24> resizedMemory))
        {
            throw new NonContiguousImageException("Image memory is not contiguous after resize/pad operations");
        }

        resizedMemory.CopyTo(dest);
    }

    /// <summary>
    /// Resizes image to exact dimensions with padding and copies directly into tensor slice (DBNet approach).
    /// Scales image to fit within bounds and pads with black to reach exact dimensions.
    /// </summary>
    public static void AspectResizeInto(Image<Rgb24> src, TensorSpan<float> tensorSlice, int destWidth, int destHeight)
    {
        var config = Configuration.Default.Clone();
        config.PreferContiguousImageBuffers = true;
        using var resized = src.Clone(config, x => x
            .Resize(new ResizeOptions
            {
                Size = new Size(destWidth, destHeight),
                Mode = ResizeMode.Pad,
                Position = AnchorPositionMode.TopLeft,
                PadColor = Color.Black,
                Sampler = KnownResamplers.Bicubic
            }));

        if (!resized.DangerousTryGetSinglePixelMemory(out Memory<Rgb24> resizedMemory))
        {
            throw new NonContiguousImageException("Image memory is not contiguous after resize/pad operations");
        }

        var pixels = resizedMemory.Span;

        // Copy directly into tensor slice in HWC layout
        for (int y = 0; y < destHeight; y++)
        {
            for (int x = 0; x < destWidth; x++)
            {
                var pixel = pixels[y * destWidth + x];
                
                // HWC layout: batch=0 (already sliced), height=y, width=x, channel=c
                ReadOnlySpan<nint> rIndices = [0, y, x, 0]; // Red channel
                ReadOnlySpan<nint> gIndices = [0, y, x, 1]; // Green channel
                ReadOnlySpan<nint> bIndices = [0, y, x, 2]; // Blue channel
                
                tensorSlice[rIndices] = pixel.R;
                tensorSlice[gIndices] = pixel.G;
                tensorSlice[bIndices] = pixel.B;
            }
        }
    }

    /// <summary>
    /// Scales image with aspect ratio preservation and manual padding (SVTRv2 approach).
    /// Scales to target height, maintains aspect ratio, then pads on the right with black.
    /// </summary>
    public static void ScaleResizeInto(Image<Rgb24> src, Memory<Rgb24> dest, int destWidth, int destHeight, int minWidth, int maxWidth)
    {
        if (destWidth * destHeight != dest.Length)
        {
            throw new ArgumentException(
                $"Expected buffer size {destWidth * destHeight}, actual size was {dest.Length}");
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

        var pixels = resizedMemory.Span;

        // Clear destination buffer to black (padding)
        dest.Span.Fill(new Rgb24(0, 0, 0));

        // Copy resized image to the left side of destination buffer
        for (int y = 0; y < destHeight; y++)
        {
            var srcRow = pixels.Slice(y * targetWidth, targetWidth);
            var destRow = dest.Span.Slice(y * destWidth, targetWidth);
            srcRow.CopyTo(destRow);
        }
    }
}

public class NonContiguousImageException : InvalidOperationException
{
    public NonContiguousImageException(string message) : base(message) { }
}
