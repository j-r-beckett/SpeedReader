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

    public static float[] PreProcess(Image<Rgb24> image, List<Rectangle> rectangles)
    {
        // Use fixed dimensions for all text regions
        const int fixedHeight = 48;
        const int fixedWidth = 320;  // Maximum width

        int totalRectangles = rectangles.Count;
        int itemSize = fixedHeight * fixedWidth * 3;

        // Allocate array directly in HWC format (no batch dimension, just concatenated items)
        float[] data = new float[totalRectangles * itemSize];

        // Process each rectangle
        for (int i = 0; i < rectangles.Count; i++)
        {
            var rect = rectangles[i];
            int targetWidth = CalculateTargetWidth(rect);

            var dest = data.AsSpan().Slice(i * itemSize, itemSize);
            Resampling.CropResizeInto(image, rect, dest, fixedWidth, fixedHeight, targetWidth);

            // Convert this item to CHW format in place
            TensorOps.NhwcToNchw(dest, [fixedHeight, fixedWidth, 3]);

            // Apply SVTRv2 normalization: [0,255] → [-1,1] for each channel
            for (int channel = 0; channel < 3; channel++)
            {
                int channelOffset = i * itemSize + channel * fixedHeight * fixedWidth;
                var channelTensor = Tensor.Create(data, channelOffset, [fixedHeight, fixedWidth], default);

                Tensor.Divide(channelTensor, 127.5f, channelTensor);
                Tensor.Subtract(channelTensor, 1.0f, channelTensor);
            }
        }

        return data;  // Each item is now in CHW format
    }

    public static string[] PostProcess(float[] modelOutput, int numRectangles)
    {
        // Model output dimensions from SVTRv2
        int sequenceLength = modelOutput.Length / numRectangles / 6625;  // Assuming 6625 vocab size
        int numClasses = 6625;

        var results = new List<string>();

        for (int i = 0; i < numRectangles; i++)
        {
            // Each rectangle's output data: [sequence_length, num_classes]
            var regionSpan = modelOutput.AsSpan().Slice(i * sequenceLength * numClasses, sequenceLength * numClasses);
            string text = CTC.DecodeSingleSequence(regionSpan, sequenceLength, numClasses);
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
}
