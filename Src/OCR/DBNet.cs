using System.Numerics.Tensors;
using System.Buffers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using OCR.Algorithms;

namespace OCR;

public static class DBNet
{
    private const float BinarizationThreshold = 0.2f;

    public static Tensor<float> PreProcess(Image<Rgb24>[] batch)
    {
        if (batch.Length == 0)
        {
            throw new ArgumentException("Batch cannot be empty", nameof(batch));
        }

        (int width, int height) = CalculateDimensions(batch);

        // Create NCHW tensor for the batch
        ReadOnlySpan<nint> shape = [batch.Length, 3, height, width];
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
                Resize.AspectResizeInto(batch[i], memory, width, height);

                // Convert single image from HWC to CHW layout in tensor
                TensorConversion.ConvertImageToNCHW(memory, tensorSpan, i, width, height);
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
        var normalizationSpan = tensor.AsTensorSpan();

        for (int channel = 0; channel < 3; channel++)
        {
            // Slice all batches, single channel, all spatial dimensions
            ReadOnlySpan<NRange> channelRange = [
                NRange.All,                           // All batches
                new NRange(channel, channel + 1),     // Single channel
                NRange.All,                           // All heights
                NRange.All                            // All widths
            ];

            var channelSlice = normalizationSpan[channelRange];

            // Subtract mean and divide by std in-place
            Tensor.Subtract(channelSlice, means[channel], channelSlice);
            Tensor.Divide(channelSlice, stds[channel], channelSlice);
        }

        return tensor;
    }

    public static List<List<(int X, int Y)>> PostProcess(Tensor<float> tensor, int originalWidth, int originalHeight)
    {
        var shape = tensor.Lengths;
        int batchSize = (int)shape[0];
        int modelHeight = (int)shape[1];
        int modelWidth = (int)shape[2];

        float scaleX = (float)originalWidth / modelWidth;
        float scaleY = (float)originalHeight / modelHeight;

        Binarization.BinarizeInPlace(tensor, BinarizationThreshold);

        // Process each batch item directly using tensor slicing - no flattening needed!
        var allComponents = new List<(int X, int Y)[]>();
        var tensorSpan = tensor.AsTensorSpan();

        for (int batchIndex = 0; batchIndex < batchSize; batchIndex++)
        {
            // Extract single batch using NRange slicing
            ReadOnlySpan<NRange> batchRange = [
                new NRange(batchIndex, batchIndex + 1), // Single batch
                NRange.All,                             // All heights
                NRange.All                              // All widths
            ];

            var batchSlice = tensorSpan[batchRange]; // Shape: [1, H, W]

            var components = ConnectedComponents.FindComponents(batchSlice);
            allComponents.AddRange(components);
        }

        var contours = new List<(int X, int Y)[]>();
        foreach (var component in allComponents)
        {
            if (component.Length >= 3)
            {
                var hull = GrahamScan.ComputeConvexHull(component);
                if (hull.Length >= 3)
                {
                    contours.Add(hull);
                }
            }
        }

        var dilatedContours = PolygonDilation.DilatePolygons(contours.ToArray());

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
