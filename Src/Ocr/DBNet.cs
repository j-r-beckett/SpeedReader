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


    public static float[] PreProcessSingle(Image<Rgb24> image)
    {
        // Use fixed dimensions for all images
        const int fixedWidth = 1344;  // 1333 rounded up to multiple of 32
        const int fixedHeight = 736;  // Already a multiple of 32
        
        // Allocate array directly in NHWC format
        float[] nhwcData = new float[1 * fixedHeight * fixedWidth * 3];
        
        // Resize image to fixed dimensions
        Resampling.AspectResizeInto(image, nhwcData, fixedWidth, fixedHeight);
        
        // Convert to NCHW format in place
        TensorOps.NhwcToNchw(nhwcData, [1, fixedHeight, fixedWidth, 3]);
        
        // Apply normalization using tensor operations
        var nchwTensor = Tensor.Create(nhwcData, [1, 3, fixedHeight, fixedWidth]);
        var tensorSpan = nchwTensor.AsTensorSpan();
        
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
        
        return nhwcData;  // This is now mutated to NCHW format
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

    public static List<Rectangle> PostProcessSingle(float[] processedImage, int originalWidth, int originalHeight)
    {
        // Fixed dimensions match PreProcessSingle
        const int fixedWidth = 1344;
        const int fixedHeight = 736;
        
        // Create span2D from the processed image data
        var probabilityMapSpan = processedImage.AsSpan().AsSpan2D(fixedHeight, fixedWidth);
        
        return PostProcess(probabilityMapSpan, originalWidth, originalHeight);
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


    private static (int width, int height) CalculateFittedDimensions(int originalWidth, int originalHeight)
    {
        double scale = Math.Min((double)1333 / originalWidth, (double)736 / originalHeight);
        int fittedWidth = (int)Math.Round(originalWidth * scale);
        int fittedHeight = (int)Math.Round(originalHeight * scale);
        return (fittedWidth, fittedHeight);
    }
}
