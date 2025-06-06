using System.Numerics.Tensors;
using OCR.Algorithms;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace OCR;

public static class SVTRv2
{
    private const int TargetHeight = 48;
    private const int MinWidth = 12;
    private const int MaxWidth = 320;

    public static Buffer<float> PreProcess(Image<Rgb24>[] batch)
    {
        if (batch.Length == 0)
        {
            throw new ArgumentException("Batch cannot be empty", nameof(batch));
        }

        int maxWidth = CalculateMaxWidth(batch);

        var buffer = new Buffer<float>(batch.Length * TargetHeight * maxWidth * 3, [batch.Length, TargetHeight, maxWidth, 3]);

        for (int i = 0; i < batch.Length; i++)
        {
            var dest = buffer.AsSpan().Slice(i * TargetHeight * maxWidth * 3, TargetHeight * maxWidth * 3);
            Resize.ScaleResizeInto(batch[i], dest, maxWidth, TargetHeight, MinWidth, MaxWidth);
        }

        TensorConversion.NHWCToNCHW(buffer);

        // Apply SVTRv2 normalization: [0,255] → [-1,1]
        // Optimized: (pixel/255 - 0.5) / 0.5 = pixel/127.5 - 1.0
        var tensor = buffer.AsTensor();
        Tensor.Divide(tensor, 127.5f, tensor);
        Tensor.Subtract(tensor, 1.0f, tensor);

        return buffer;
    }

    public static string[] PostProcess(Buffer<float> buffer)
    {
        var results = new List<string>();
        int batchSize = (int)buffer.Shape[0];
        int sequenceLength = (int)buffer.Shape[1];
        int numClasses = (int)buffer.Shape[2];

        var bufferSpan = buffer.AsSpan();

        for (int batch = 0; batch < batchSize; batch++)
        {
            // Slice the span for this batch: [batch, :, :]
            var batchSpan = bufferSpan.Slice(batch * sequenceLength * numClasses, sequenceLength * numClasses);
            string text = CTC.DecodeSingleSequence(batchSpan, sequenceLength, numClasses);
            results.Add(text);
        }

        return results.ToArray();
    }

    internal static int CalculateMaxWidth(Image<Rgb24>[] batch)
    {
        int maxWidth = MinWidth;

        foreach (var image in batch)
        {
            // Calculate target width maintaining aspect ratio with fixed height of 48px
            double aspectRatio = (double)image.Width / image.Height;
            int targetWidth = (int)Math.Round(aspectRatio * TargetHeight);

            // Clamp to reasonable bounds
            targetWidth = Math.Max(MinWidth, Math.Min(MaxWidth, targetWidth));
            maxWidth = Math.Max(maxWidth, targetWidth);
        }

        return maxWidth;
    }


}
