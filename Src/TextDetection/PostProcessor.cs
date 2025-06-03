using CommunityToolkit.HighPerformance;
using System.Numerics.Tensors;
using System.Buffers;

namespace TextDetection;

public class PostProcessor
{
    private const float BinarizationThreshold = 0.2f;

    public List<List<(int X, int Y)>> PostProcess(Tensor<float> tensor, int originalWidth, int originalHeight)
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

            var components = ConnectedComponentAnalysis.FindComponents(batchSlice);
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
}
