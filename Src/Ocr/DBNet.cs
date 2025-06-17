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

        var buffer = new Buffer<float>(batch.Length * 3 * height * width, [batch.Length, height, width, 3]);

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

    internal static List<Rectangle>[] PostProcess(Buffer<float> batch, Image<Rgb24>[] originalBatch)
    {
        int n = (int)batch.Shape[0];
        int height = (int)batch.Shape[1];
        int width = (int)batch.Shape[2];
        int size = height * width;

        List<Rectangle>[] results = new List<Rectangle>[n];

        // Binarize for connected component analysis
        Thresholding.BinarizeInPlace(batch.AsTensor(), BinarizationThreshold);

        for (int i = 0; i < n; i++)
        {
            var probabilityMap = batch.AsSpan().Slice(i * size, size).AsSpan2D(height, width);
            var components = ConnectedComponents.FindComponents(probabilityMap);
            List<Rectangle> boundingBoxes = [];
            (int originalWidth, int originalHeight) = (originalBatch[i].Width, originalBatch[i].Height);

            // Calculate the actual fitted dimensions (without padding) for this image
            var (batchWidth, batchHeight) = CalculateDimensions(originalBatch);

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

                // Scale to original coordinates using fitted dimensions
                double scale = Math.Max((double)originalWidth / batchWidth, (double)originalHeight / batchHeight);
                Scale(dilatedPolygon, scale, scale);
                var boundingBox = GetBoundingBox(dilatedPolygon);
                boundingBoxes.Add(boundingBox);
            }

            results[i] = boundingBoxes;
        }

        return results;
    }

    internal static void Scale(List<(int X, int Y)> polygon, double scaleX, double scaleY)
    {
        for (int i = 0; i < polygon.Count; i++)
        {
            int originalX = (int)Math.Round(polygon[i].X * scaleX);
            int originalY = (int)Math.Round(polygon[i].Y * scaleY);
            polygon[i] = (originalX, originalY);
        }
    }

    internal static Rectangle GetBoundingBox(List<(int X, int Y)> polygon)
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

        return new Rectangle(minX, minY, maxX - minX, maxY - minY);
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
