using System.Numerics.Tensors;
using System.Buffers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace TextDetection;

public class Preprocessor
{
    public static Tensor<float> Preprocess(Image<Rgb24>[] batch)
    {
        if (batch.Length == 0)
        {
            throw new ArgumentException("Batch cannot be empty", nameof(batch));
        }

        (int width, int height) = CalculateDimensions(batch);

        // Create NCHW tensor for the batch
        ReadOnlySpan<nint> shape = [(nint)batch.Length, 3, (nint)height, (nint)width];
        var data = new float[batch.Length * 3 * height * width];
        var tensor = Tensor.Create(data, shape);
        var tensorSpan = tensor.AsTensorSpan();

        // Create temporary pixel buffer for each image using ArrayPool
        var pixelBuffer = ArrayPool<Rgb24>.Shared.Rent(width * height);
        try
        {
            var memory = pixelBuffer.AsMemory(0, width * height);

            for (int i = 0; i < batch.Length; i++)
            {
                AspectResizeInto(batch[i], memory, width, height);

                // Copy pixels from HWC to CHW layout in tensor
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        var pixel = memory.Span[y * width + x];

                        // Convert HWC to CHW: batch, channel, height, width
                        ReadOnlySpan<nint> rIndices = [i, 0, y, x]; // Red channel
                        ReadOnlySpan<nint> gIndices = [i, 1, y, x]; // Green channel
                        ReadOnlySpan<nint> bIndices = [i, 2, y, x]; // Blue channel

                        tensorSpan[rIndices] = pixel.R;
                        tensorSpan[gIndices] = pixel.G;
                        tensorSpan[bIndices] = pixel.B;
                    }
                }
            }
        }
        finally
        {
            ArrayPool<Rgb24>.Shared.Return(pixelBuffer);
        }

        // Apply normalization per channel using tensor operations
        // Model-specific normalization parameters from pipeline.json
        float[] means = [123.675f, 116.28f, 103.53f];
        float[] stds = [58.395f, 57.12f, 57.375f];

        for (int channel = 0; channel < 3; channel++)
        {
            // Slice all batches, single channel, all spatial dimensions
            ReadOnlySpan<NRange> channelRange = [
                NRange.All,                           // All batches
                new NRange(channel, channel + 1),     // Single channel
                NRange.All,                           // All heights
                NRange.All                            // All widths
            ];

            var channelSlice = tensorSpan[channelRange];

            // Subtract mean and divide by std in-place
            Tensor.Subtract(channelSlice, means[channel], channelSlice);
            Tensor.Divide(channelSlice, stds[channel], channelSlice);
        }

        return tensor;
    }

    internal static (int width, int height) CalculateDimensions(Image<Rgb24>[] batch)
    {
        int maxWidth = -1;
        int maxHeight = -1;

        foreach (var image in batch)
        {
            int width = image.Width;
            int height = image.Height;
            double scale = Math.Min((double)1333 / width, (double)736 / height);
            int fittedWidth = (int)Math.Round(width * scale);
            int fittedHeight = (int)Math.Round(height * scale);
            int paddedWidth = (fittedWidth + 31) / 32 * 32;
            int paddedHeight = (fittedHeight + 31) / 32 * 32;
            maxWidth = Math.Max(maxWidth, paddedWidth);
            maxHeight = Math.Max(maxHeight, paddedHeight);
        }

        return (maxWidth, maxHeight);
    }

    // src is scaled up or scaled down so that it just barely fits inside a destWidth x destHeight box
    // pixels left uncovered by src are black
    internal static void AspectResizeInto(Image<Rgb24> src, Memory<Rgb24> dest, int destWidth, int destHeight)
    {
        if (destWidth * destHeight != dest.Length)
        {
            throw new ArgumentException(
                $"Expected buffer size {destWidth * destHeight}, actual size was {dest.Length}");
        }

        // TODO: ImageSharp doesn't have a ResizeInto equivalent. Implement in raw memory, writing directly to dest
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
}

public class NonContiguousImageException : InvalidOperationException
{
    public NonContiguousImageException(string message) : base(message) { }
}
