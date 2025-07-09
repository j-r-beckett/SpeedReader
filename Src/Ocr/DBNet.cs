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

    private const int Width = 1344;
    private const int Height = 736;

    public static float[] PreProcess(Image<Rgb24> image)
    {
        float[] data = new float[Height * Width * 3];

        // Resize
        Resampling.AspectResizeInto(image, data, Width, Height);

        // Convert to CHW format
        TensorOps.NhwcToNchw(data, [Height, Width, 3]);

        // Apply ImageNet normalization
        float[] means = [123.675f, 116.28f, 103.53f];
        float[] stds = [58.395f, 57.12f, 57.375f];

        for (int channel = 0; channel < 3; channel++)
        {
            var tensor = Tensor.Create(data, channel * Height * Width, [Height, Width], default);

            // Subtract mean and divide by std in place
            Tensor.Subtract(tensor, means[channel], tensor);
            Tensor.Divide(tensor, stds[channel], tensor);
        }

        return data;
    }

    public static List<Rectangle> PostProcess(float[] processedImage, int originalWidth, int originalHeight)
    {
        // Create span2D from the processed image data
        var probabilityMapSpan = processedImage.AsSpan().AsSpan2D(Height, Width);

        var components = ConnectedComponents.FindComponents(probabilityMapSpan, 0.2f);
        List<Rectangle> boundingBoxes = [];

        foreach (var connectedComponent in components)
        {
            // Skip very small components
            if (connectedComponent.Length < 10)
            {
                continue;
            }

            // Construct convex hull
            var polygon = ConvexHull.GrahamScan(connectedComponent);

            // Dilate the polygon
            var dilatedPolygon = Dilation.DilatePolygon(polygon);

            // Convert back to original coordinate system
            double scale = Math.Max((double)originalWidth / probabilityMapSpan.Width, (double)originalHeight / probabilityMapSpan.Height);
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
}
