using System.Buffers;
using System.Numerics.Tensors;
using CommunityToolkit.HighPerformance;
using Ocr.Algorithms;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ocr;

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

        var buffer = new Buffer<float>([batch.Length, height, width, 3]);

        for (int i = 0; i < batch.Length; i++)
        {
            var dest = buffer.AsSpan().Slice(i * width * height * 3, width * height * 3);
            Resampling.AspectResizeInto(batch[i], dest, width, height);
        }

        // Convert to NCHW in place and update Shape
        TensorOps.NhwcToNchw(buffer);

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

    internal static List<Rectangle> PostProcess(Span2D<float> probabilityMap, int originalWidth, int originalHeight)
    {
        var components = ConnectedComponents.FindComponents(probabilityMap, 0.2f);
        List<Rectangle> boundingBoxes = [];

        foreach (var connectedComponent in components)
        {
            // Skip very small components
            if (connectedComponent.Length < 10)
            {
                continue;
            }

            var polygon = ConvexHull.GrahamScan(connectedComponent);

            // Dilate the polygon
            var dilatedPolygon = Dilation.DilatePolygon(polygon);
            if (dilatedPolygon.Count == 0)
            {
                continue;
            }

            double scale = Math.Max((double)originalWidth / probabilityMap.Width, (double)originalHeight / probabilityMap.Height);
            Scale(dilatedPolygon, scale);
            boundingBoxes.Add(GetBoundingBox(dilatedPolygon, originalWidth, originalHeight));
        }

        return boundingBoxes;
    }

    private static void Scale(List<(int X, int Y)> polygon, double scale)
    {
        for (int i = 0; i < polygon.Count; i++)
        {
            int originalX = (int)Math.Round(polygon[i].X * scale);
            int originalY = (int)Math.Round(polygon[i].Y * scale);
            polygon[i] = (originalX, originalY);
        }
    }

    internal static Rectangle GetBoundingBox(List<(int X, int Y)> polygon, int imageWidth, int imageHeight)
    {
        int maxX = int.MinValue;
        int maxY = int.MinValue;
        int minX = int.MaxValue;
        int minY = int.MaxValue;

        foreach ((int x, int y) in polygon)
        {
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
        }

        // Clamp coordinates to image bounds
        minX = Math.Max(0, minX);
        minY = Math.Max(0, minY);
        maxX = Math.Min(imageWidth - 1, maxX);
        maxY = Math.Min(imageHeight - 1, maxY);

        return new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    internal static (int width, int height) CalculateDimensions(Image<Rgb24>[] batch)
    {
        int maxWidth = -1;
        int maxHeight = -1;

        foreach (var image in batch)
        {
            var (fittedWidth, fittedHeight) = CalculateFittedDimensions(image.Width, image.Height);
            int paddedWidth = (fittedWidth + 31) / 32 * 32;
            int paddedHeight = (fittedHeight + 31) / 32 * 32;
            maxWidth = Math.Max(maxWidth, paddedWidth);
            maxHeight = Math.Max(maxHeight, paddedHeight);
        }

        return (maxWidth, maxHeight);
    }

    private static (int width, int height) CalculateFittedDimensions(int originalWidth, int originalHeight)
    {
        double scale = Math.Min((double)1333 / originalWidth, (double)736 / originalHeight);
        int fittedWidth = (int)Math.Round(originalWidth * scale);
        int fittedHeight = (int)Math.Round(originalHeight * scale);
        return (fittedWidth, fittedHeight);
    }
}
