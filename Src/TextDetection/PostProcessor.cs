using CommunityToolkit.HighPerformance;
using System.Numerics.Tensors;

namespace TextDetection;

public class PostProcessor
{
    private const float BinarizationThreshold = 0.2f;

    public List<List<(int X, int Y)>> PostProcess(Tensor<float> tensor, int originalWidth, int originalHeight)
    {
        var probabilityMaps = TensorOps.ExtractProbabilityMaps(tensor);
        var probabilityMap = probabilityMaps[0];

        int modelHeight = probabilityMap.GetLength(0);
        int modelWidth = probabilityMap.GetLength(1);

        float scaleX = (float)originalWidth / modelWidth;
        float scaleY = (float)originalHeight / modelHeight;

        var probabilitySpan = new Span2D<float>(probabilityMap);

        Binarization.BinarizeInPlace(probabilitySpan, BinarizationThreshold);

        var components = ConnectedComponentAnalysis.FindComponents(probabilitySpan);

        var contours = new List<(int X, int Y)[]>();
        foreach (var component in components)
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
