using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace TextDetection;

/// <summary>
/// Represents a preprocessed image ready for DBNet text detection inference.
///
/// FUTURE OPTIMIZATION: This implementation currently allocates a new float array for each image.
/// For high-throughput scenarios, this can be optimized with a shared buffer pool approach:
///
/// 1. Maintain a thread-safe pool of reusable float[] buffers sized for max expected dimensions
/// 2. Create() borrows a temporary buffer from the pool for HWC→CHW conversion
/// 3. Copy the normalized CHW data back into the original image's memory (reinterpreted as float[])
/// 4. DbNetImage takes ownership of the image memory via ReadOnlyMemory&lt;float&gt; reinterpretation
/// 5. Make DbNetImage disposable to manage image lifetime
/// 6. Integrate with TextDetectorInput to validate expected dimensions and provide initializers
///
/// This would eliminate per-image allocations while maintaining the CHW layout required by TextDetector.
/// </summary>
public readonly struct DbNetImage
{
    private readonly ReadOnlyMemory<float> _normalizedData;
    public ReadOnlySpan<float> Data => _normalizedData.Span;
    public int Width { get; }
    public int Height { get; }

    private DbNetImage(ReadOnlyMemory<float> data, int width, int height)
    {
        _normalizedData = data;
        Width = width;
        Height = height;
    }

    public static DbNetImage Create(Image<Rgb24> image)
    {
        // Step 1: Resize with aspect ratio preservation to fit within [1333, 736]
        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new Size(1333, 736),
            Mode = ResizeMode.Max,
            Sampler = KnownResamplers.Bicubic
        }));

        // Step 2: Pad to make dimensions divisible by 32
        int paddedWidth = (image.Width + 31) / 32 * 32;
        int paddedHeight = (image.Height + 31) / 32 * 32;

        image.Mutate(x => x.Pad(paddedWidth, paddedHeight, Color.Black));

        // Step 3: Extract contiguous pixel memory or throw
        if (!image.DangerousTryGetSinglePixelMemory(out Memory<Rgb24> pixelMemory))
        {
            throw new NonContiguousImageException("Image memory is not contiguous after resize/pad operations");
        }

        // Step 4: Convert HWC → CHW with normalization
        float[] normalizedData = new float[3 * image.Width * image.Height];
        float[] means = [ 123.675f, 116.28f, 103.53f ];
        float[] stds = [ 58.395f, 57.12f, 57.375f ];

        int channelSize = image.Width * image.Height;

        for (int i = 0; i < pixelMemory.Span.Length; i++)
        {
            var pixel = pixelMemory.Span[i];

            normalizedData[i] = (pixel.R - means[0]) / stds[0];
            normalizedData[channelSize + i] = (pixel.G - means[1]) / stds[1];
            normalizedData[2 * channelSize + i] = (pixel.B - means[2]) / stds[2];
        }

        return new DbNetImage(normalizedData, image.Width, image.Height);
    }
}

public class NonContiguousImageException : InvalidOperationException
{
    public NonContiguousImageException(string message) : base(message) { }
}
