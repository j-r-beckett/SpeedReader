using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace TextDetection;

public class Preprocessor
{
    public static OrtValue Preprocess(Image<Rgb24>[] batch)
    {
        if (batch.Length == 0)
        {
            throw new ArgumentException("Batch cannot be empty", nameof(batch));
        }

        // Calculate target dimensions from first image
        (int width, int height) = CalculateDimensions(batch);

        // Create tensor for the batch
        long[] shape = [batch.Length, 3, height, width];
        var tensor = OrtValue.CreateAllocatedTensorValue(OrtAllocator.DefaultInstance, TensorElementType.Float, shape);
        var tensorSpan = tensor.GetTensorMutableDataAsSpan<float>();

        // TODO: use a memory pool
        Memory<Rgb24> memory = new (new Rgb24[width * height]);

        for (int i = 0; i < batch.Length; i++)
        {
            AspectResizeInto(batch[i], memory, width, height);

            // Convert HWC -> CHW with normalization and copy directly to tensor
            int batchOffset = i * width * height * 3;
            int channelOffset = width * height;

            // Model-specific normalization parameters from pipeline.json
            float[] means = [123.675f, 116.28f, 103.53f];
            float[] stds = [58.395f, 57.12f, 57.375f];

            for (int j = 0; j < memory.Span.Length; j++)
            {
                var pixel = memory.Span[j];

                tensorSpan[batchOffset + j] = (pixel.R - means[0]) / stds[0];
                tensorSpan[batchOffset + channelOffset + j] = (pixel.G - means[1]) / stds[1];
                tensorSpan[batchOffset + 2 * channelOffset + j] = (pixel.B - means[2]) / stds[2];
            }
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
                Mode = ResizeMode.Max,
                Sampler = KnownResamplers.Bicubic
            })
            .Pad(destWidth, destHeight, Color.Black));

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
