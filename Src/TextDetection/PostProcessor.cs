using CommunityToolkit.HighPerformance;
using System.Numerics.Tensors;

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

        // Extract probability maps after binarization
        var probabilityMaps = TensorOps.ExtractProbabilityMaps(tensor);
        
        var allComponents = new List<(int X, int Y)[]>();

        // Process each batch item
        for (int batchIndex = 0; batchIndex < batchSize; batchIndex++)
        {
            var probabilityMap = probabilityMaps[batchIndex];
            var probabilitySpan = new Span2D<float>(probabilityMap);
            var components = ConnectedComponentAnalysis.FindComponents(probabilitySpan);
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
