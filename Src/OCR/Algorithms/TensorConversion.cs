using System.Buffers;
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

    public static nint[] NHWCToNCHW<T>(Buffer<T> buffer, nint[] nhwcShape) where T : unmanaged
    {
        var tensor = buffer.AsTensor(nhwcShape);

        if (nhwcShape.Length != 4)
        {
            throw new ArgumentException($"Tensor has {tensor.Lengths.Length} dimensions, expected 4 (NHWC)");
        }

        int batchSize = (int)(nhwcShape[1] * nhwcShape[2] * nhwcShape[3]);
        T[] tempBuffer = ArrayPool<T>.Shared.Rent(batchSize);
        var tempTensor = Tensor.Create(tempBuffer, [nhwcShape[3], nhwcShape[1], nhwcShape[2]]);
        try
        {
            // Convert from NHWC to NCHW
            // TODO: refactor to a block-based memory-access pattern
            for (int n = 0; n < nhwcShape[0]; n++) // batch
            {
                for (int h = 0; h < nhwcShape[1]; h++)      // height
                for (int w = 0; w < nhwcShape[2]; w++)      // width
                for (int c = 0; c < nhwcShape[3]; c++)      // channel
                {
                    tempTensor[[c, h, w]] = tensor[[n, h, w, c]];
                }

                tempTensor.FlattenTo(buffer.AsSpan().Slice(n * batchSize, batchSize));
            }
        }
        finally
        {
            ArrayPool<T>.Shared.Return(tempBuffer);
        }

        return [nhwcShape[0], nhwcShape[3], nhwcShape[1], nhwcShape[2]];
    }
}
