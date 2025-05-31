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

        // Model-specific normalization parameters from pipeline.json
        float[] means = [123.675f, 116.28f, 103.53f];
        float[] stds = [58.395f, 57.12f, 57.375f];

        // Calculate target dimensions from first image
        var (width, height) = CalculateDimensions(batch[0]);

        // Create tensor for the batch
        long[] shape = [batch.Length, 3, height, width];
        var tensor = OrtValue.CreateAllocatedTensorValue(OrtAllocator.DefaultInstance, TensorElementType.Float, shape);
        var tensorSpan = tensor.GetTensorMutableDataAsSpan<float>();

        int imageSizeBytes = width * height * 3;
        int channelSize = width * height;

        for (int i = 0; i < batch.Length; i++)
        {
            // Clone and preprocess each image
            using var processed = batch[i].Clone();

            // Step 1: Resize with aspect ratio preservation
            processed.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(1333, 736),
                Mode = ResizeMode.Max,
                Sampler = KnownResamplers.Bicubic
            }));

            // Step 2: Pad to make dimensions divisible by 32
            int paddedWidth = (processed.Width + 31) / 32 * 32;
            int paddedHeight = (processed.Height + 31) / 32 * 32;
            processed.Mutate(x => x.Pad(paddedWidth, paddedHeight, Color.Black));

            // Validate dimensions match across batch
            if (processed.Width != width || processed.Height != height)
            {
                throw new InvalidOperationException(
                    $"Processed image has size {processed.Width}x{processed.Height}, expected size is {width}x{height}");
            }

            // Extract contiguous pixel memory
            if (!processed.DangerousTryGetSinglePixelMemory(out Memory<Rgb24> pixelMemory))
            {
                throw new NonContiguousImageException("Image memory is not contiguous after resize/pad operations");
            }

            // Convert HWC -> CHW with normalization and copy directly to tensor
            int batchOffset = i * imageSizeBytes;

            for (int j = 0; j < pixelMemory.Span.Length; j++)
            {
                var pixel = pixelMemory.Span[j];

                tensorSpan[batchOffset + j] = (pixel.R - means[0]) / stds[0];
                tensorSpan[batchOffset + channelSize + j] = (pixel.G - means[1]) / stds[1];
                tensorSpan[batchOffset + 2 * channelSize + j] = (pixel.B - means[2]) / stds[2];
            }
        }

        return tensor;
    }

    public static (int width, int height) CalculateDimensions(Image image)
    {
        int width = image.Width;
        int height = image.Height;
        double scale = Math.Min((double)1333 / width, (double)736 / height);
        int fittedWidth = (int)Math.Round(width * scale);
        int fittedHeight = (int)Math.Round(height * scale);
        int paddedWidth = (fittedWidth + 31) / 32 * 32;
        int paddedHeight = (fittedHeight + 31) / 32 * 32;
        return (paddedWidth, paddedHeight);
    }

    internal static class Functions
    {
        // internal static (int width, int height) CalculateDimensions(Image<Rgb24> sampleImage) {}
    }
}

public class NonContiguousImageException : InvalidOperationException
{
    public NonContiguousImageException(string message) : base(message) { }
}
