using System.Numerics.Tensors;
using Ocr.Algorithms;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ocr;

public static class SVTRv2
{
    private const int TargetHeight = 48;
    private const int MinWidth = 12;
    private const int MaxWidth = 320;

    public static Buffer<float> PreProcess(Image<Rgb24>[] images, List<Rectangle>[] rectangles)
    {
        if (images.Length == 0)
        {
            throw new ArgumentException("Images cannot be empty", nameof(images));
        }

        if (images.Length != rectangles.Length)
        {
            throw new ArgumentException("Images and rectangles arrays must have the same length", nameof(rectangles));
        }

        // Validate rectangles and count total
        int totalRectangles = 0;
        for (int i = 0; i < images.Length; i++)
        {
            totalRectangles += rectangles[i].Count;
        }

        // Calculate max width across all rectangles
        int maxWidth = CalculateMaxWidth(rectangles);

        var buffer = new Buffer<float>(totalRectangles * TargetHeight * maxWidth * 3, [totalRectangles, TargetHeight, maxWidth, 3]);

        // Process each image and its rectangles
        int bufferIndex = 0;
        for (int i = 0; i < images.Length; i++)
        {
            for (int j = 0; j < rectangles[i].Count; j++)
            {
                var rect = rectangles[i][j];
                int targetWidth = CalculateTargetWidth(rect);

                var dest = buffer.AsSpan().Slice(bufferIndex * TargetHeight * maxWidth * 3, TargetHeight * maxWidth * 3);
                Resampling.CropResizeInto(images[i], rect, dest, maxWidth, TargetHeight, targetWidth);
                bufferIndex++;
            }
        }

        TensorOps.NhwcToNchw(buffer);

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

    internal static int CalculateTargetWidth(Rectangle rect)
    {
        // Calculate target width maintaining aspect ratio of the cropped region
        double aspectRatio = (double)rect.Width / rect.Height;
        int targetWidth = (int)Math.Round(aspectRatio * TargetHeight);

        // Clamp to reasonable bounds
        return Math.Max(MinWidth, Math.Min(MaxWidth, targetWidth));
    }

    internal static int CalculateMaxWidth(List<Rectangle>[] rectangles)
    {
        int maxWidth = MinWidth;

        foreach (var rectList in rectangles)
        {
            foreach (var rect in rectList)
            {
                int targetWidth = CalculateTargetWidth(rect);
                maxWidth = Math.Max(maxWidth, targetWidth);
            }
        }

        return maxWidth;
    }

}
