using System.Threading.Tasks.Dataflow;
using Ocr.Visualization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ocr.Blocks;

public static class OcrPostProcessingBlock
{
    public static IPropagatorBlock<(Image<Rgb24>, List<Rectangle>, List<string>, VizBuilder), (Image<Rgb24>, List<Rectangle>, List<string>, VizBuilder)> Create()
    {
        return new TransformBlock<(Image<Rgb24> Image, List<Rectangle> Rectangles, List<string> Texts, VizBuilder VizBuilder), (Image<Rgb24>, List<Rectangle>, List<string>, VizBuilder)>(data =>
        {
            var (filteredRectangles, filteredTexts) = RemoveEmptyText(data.Rectangles, data.Texts);
            var (mergedRectangles, mergedTexts) = MergeLines(filteredRectangles, filteredTexts);

            data.VizBuilder.AddMergedResults(mergedRectangles, mergedTexts);

            return (data.Image, mergedRectangles, mergedTexts, data.VizBuilder);
        });
    }

    private static (List<Rectangle>, List<string>) RemoveEmptyText(List<Rectangle> rectangles, List<string> texts)
    {
        var filteredRectangles = new List<Rectangle>();
        var filteredTexts = new List<string>();

        for (int i = 0; i < texts.Count; i++)
        {
            var cleanedText = new string(texts[i].Where(c => c <= 127).ToArray()).Trim();
            if (cleanedText != string.Empty)
            {
                filteredRectangles.Add(rectangles[i]);
                filteredTexts.Add(cleanedText);
            }
        }

        return (filteredRectangles, filteredTexts);
    }

    private static (List<Rectangle>, List<string>) MergeLines(List<Rectangle> rectangles, List<string> texts)
    {
        if (rectangles.Count == 0)
            return (rectangles, texts);

        // Build adjacency list for connected components
        var adjacency = new List<List<int>>();
        for (int i = 0; i < rectangles.Count; i++)
        {
            adjacency.Add(new List<int>());
        }

        // Check all pairs to build edges (O(n^2))
        for (int i = 0; i < rectangles.Count; i++)
        {
            for (int j = i + 1; j < rectangles.Count; j++)
            {
                if (CanMerge(rectangles[i], rectangles[j]))
                {
                    adjacency[i].Add(j);
                    adjacency[j].Add(i);
                }
            }
        }

        // Find connected components using DFS
        var visited = new bool[rectangles.Count];
        var components = new List<List<int>>();

        for (int i = 0; i < rectangles.Count; i++)
        {
            if (!visited[i])
            {
                var component = new List<int>();
                DFS(i, adjacency, visited, component);
                components.Add(component);
            }
        }

        // Merge each component into a single rectangle and text
        var mergedRectangles = new List<Rectangle>();
        var mergedTexts = new List<string>();

        foreach (var component in components)
        {
            // Sort component by X position for proper text ordering
            component.Sort((a, b) => rectangles[a].X.CompareTo(rectangles[b].X));

            var componentRects = component.Select(i => rectangles[i]).ToList();
            var componentTexts = component.Select(i => texts[i]).ToList();

            mergedRectangles.Add(MergeRectangles(componentRects));
            mergedTexts.Add(string.Join(" ", componentTexts));
        }

        return (mergedRectangles, mergedTexts);
    }

    private static bool CanMerge(Rectangle rect1, Rectangle rect2)
    {
        // Check if rectangles are on same line (within 2x height)
        var midY1 = rect1.Y + rect1.Height / 2;
        var midY2 = rect2.Y + rect2.Height / 2;
        var maxHeight = Math.Max(rect1.Height, rect2.Height);

        if (Math.Abs(midY1 - midY2) > 2 * maxHeight)
            return false;

        // Check proximity in both directions
        return ShouldMerge(rect1, rect2) || ShouldMerge(rect2, rect1);
    }

    private static void DFS(int node, List<List<int>> adjacency, bool[] visited, List<int> component)
    {
        visited[node] = true;
        component.Add(node);

        foreach (var neighbor in adjacency[node])
        {
            if (!visited[neighbor])
            {
                DFS(neighbor, adjacency, visited, component);
            }
        }
    }

    private static bool ShouldMerge(Rectangle currentRect, Rectangle candidateRect)
    {
        var height = currentRect.Height;

        var currentTopRight = new Point(currentRect.Right, currentRect.Top);
        var currentBottomRight = new Point(currentRect.Right, currentRect.Bottom);
        var candidateTopLeft = new Point(candidateRect.Left, candidateRect.Top);
        var candidateBottomLeft = new Point(candidateRect.Left, candidateRect.Bottom);

        var topDistance = AnisotropicDistance(currentTopRight, candidateTopLeft, height);
        var bottomDistance = AnisotropicDistance(currentBottomRight, candidateBottomLeft, height);

        return topDistance <= 1.0 && bottomDistance <= 1.0;
    }

    private static double AnisotropicDistance(Point p1, Point p2, int referenceHeight)
    {
        var dx = Math.Abs(p1.X - p2.X);
        var dy = Math.Abs(p1.Y - p2.Y);

        // Normalize by reference height
        var normalizedDx = dx / (double)referenceHeight;
        var normalizedDy = dy / (double)referenceHeight;

        // Weight vertical distance more heavily than horizontal
        // This allows more horizontal gap (up to ~1.5x height) while keeping vertical tolerance tight
        var horizontalWeight = 0.67;  // More lenient
        var verticalWeight = 2.0;     // More strict

        return Math.Sqrt(horizontalWeight * normalizedDx * normalizedDx +
                        verticalWeight * normalizedDy * normalizedDy);
    }

    private static Rectangle MergeRectangles(List<Rectangle> rectangles)
    {
        if (rectangles.Count == 0)
            return Rectangle.Empty;

        var minX = rectangles.Min(r => r.Left);
        var minY = rectangles.Min(r => r.Top);
        var maxX = rectangles.Max(r => r.Right);
        var maxY = rectangles.Max(r => r.Bottom);

        return Rectangle.FromLTRB(minX, minY, maxX, maxY);
    }
}
