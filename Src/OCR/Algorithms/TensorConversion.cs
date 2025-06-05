using System.Numerics.Tensors;
using SixLabors.ImageSharp.PixelFormats;

namespace OCR.Algorithms;

public static class TensorConversion
{
    /// <summary>
    /// Converts pixels from HWC format to NCHW tensor format for a single image in a batch.
    /// </summary>
    /// <param name="pixels">Source pixel memory in HWC format</param>
    /// <param name="tensorSpan">Output tensor span in NCHW format</param>
    /// <param name="batchIndex">Index of this image in the batch</param>
    /// <param name="width">Width of the image</param>
    /// <param name="height">Height of the image</param>
    public static void ConvertImageToNCHW(
        ReadOnlyMemory<Rgb24> pixels,
        TensorSpan<float> tensorSpan,
        int batchIndex,
        int width,
        int height)
    {
        var pixelSpan = pixels.Span;

        // Copy pixels from HWC to CHW layout in tensor
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var pixel = pixelSpan[y * width + x];

                // Convert HWC to CHW: batch, channel, height, width
                ReadOnlySpan<nint> rIndices = [batchIndex, 0, y, x]; // Red channel
                ReadOnlySpan<nint> gIndices = [batchIndex, 1, y, x]; // Green channel
                ReadOnlySpan<nint> bIndices = [batchIndex, 2, y, x]; // Blue channel

                tensorSpan[rIndices] = pixel.R;
                tensorSpan[gIndices] = pixel.G;
                tensorSpan[bIndices] = pixel.B;
            }
        }
    }

}