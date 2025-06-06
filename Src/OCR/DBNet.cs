using System.Buffers;
using System.Numerics.Tensors;
using CommunityToolkit.HighPerformance;
using OCR.Algorithms;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace OCR;

public static class DBNet
{
    private const float BinarizationThreshold = 0.2f;

    public static Buffer<float> PreProcess(Image<Rgb24>[] batch)
    {
        if (batch.Length == 0)
        {
            throw new ArgumentException("Batch cannot be empty", nameof(batch));
        }

        (int width, int height) = CalculateDimensions(batch);

        var buffer = new Buffer<float>(batch.Length * 3 * height * width, [batch.Length, height, width, 3]);

        for (int i = 0; i < batch.Length; i++)
        {
            var dest = buffer.AsSpan().Slice(i * width * height * 3, width * height * 3);
            Resampling.AspectResizeInto(batch[i], dest, width, height);
        }

        // Convert to NCHW in place and update Shape
        TensorOps.NHWCToNCHW(buffer);

        // Normalize each channel using tensor operations
        var tensor = buffer.AsTensor();
        var tensorSpan = tensor.AsTensorSpan();

        float[] means = [123.675f, 116.28f, 103.53f];
        float[] stds = [58.395f, 57.12f, 57.375f];

        for (int channel = 0; channel < 3; channel++)
        {
            // Range over a single color channel
            ReadOnlySpan<NRange> channelRange = [
                NRange.All,                           // All batches
                new (channel, channel + 1),     // One channel
                NRange.All,                           // All heights
                NRange.All                            // All widths
            ];

            var channelSlice = tensorSpan[channelRange];

            // Subtract mean and divide by std in place
            Tensor.Subtract(channelSlice, means[channel], channelSlice);
            Tensor.Divide(channelSlice, stds[channel], channelSlice);
        }

        return buffer;
    }

    public static List<List<(int X, int Y)>> PostProcess(Buffer<float> buffer, int originalWidth, int originalHeight)
    {
        var tensor = buffer.AsTensor();
        var shape = tensor.Lengths;
        int batchSize = (int)shape[0];
        int modelHeight = (int)shape[1];
        int modelWidth = (int)shape[2];

        float scaleX = (float)originalWidth / modelWidth;
        float scaleY = (float)originalHeight / modelHeight;

        Thresholding.BinarizeInPlace(tensor, BinarizationThreshold);

        // Process each batch item using Span2D for efficient 2D access
        var allComponents = new List<(int X, int Y)[]>();
        var bufferSpan = buffer.AsSpan();
        int imageSize = modelHeight * modelWidth;

        for (int batchIndex = 0; batchIndex < batchSize; batchIndex++)
        {
            // Extract 2D span for this batch
            var batchSpan = bufferSpan.Slice(batchIndex * imageSize, imageSize);
            var probabilityMap = batchSpan.AsSpan2D(modelHeight, modelWidth);

            var components = ConnectedComponents.FindComponents(probabilityMap);
            allComponents.AddRange(components);
        }

        var contours = new List<(int X, int Y)[]>();
        foreach (var component in allComponents)
        {
            if (component.Length >= 3)
            {
                var hull = ConvexHull.GrahamScan(component);
                if (hull.Length >= 3)
                {
                    contours.Add(hull);
                }
            }
        }

        var dilatedContours = Dilation.DilatePolygons(contours.ToArray());

        var resultPolygons = new List<List<(int X, int Y)>>();
        foreach (var polygon in dilatedContours)
        {
            var scaledPolygon = new List<(int X, int Y)>();
            foreach (var point in polygon)
            {
                int originalX = (int)Math.Round(point.X * scaleX);
                int originalY = (int)Math.Round(point.Y * scaleY);

                originalX = Math.Clamp(originalX, 0, originalWidth - 1);
                originalY = Math.Clamp(originalY, 0, originalHeight - 1);

                scaledPolygon.Add((originalX, originalY));
            }

            if (scaledPolygon.Count >= 3)
            {
                resultPolygons.Add(scaledPolygon);
            }
        }

        return resultPolygons;
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

}
