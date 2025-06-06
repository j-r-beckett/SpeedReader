using System.Diagnostics;
using CommunityToolkit.HighPerformance;

namespace OCR.Algorithms;

public static class ConnectedComponents
{
    /// <summary>
    /// Finds all connected components in a binary probability map using 8-connectivity.
    /// Uses flood fill algorithm to identify contiguous regions of positive values.
    /// </summary>
    /// <param name="probabilityMap">2D probability map where positive values indicate foreground pixels</param>
    /// <returns>List of connected components, each containing the coordinates of pixels in that component</returns>
    /// <remarks>
    /// The input probability map is modified during processing - positive values are set to 0 as they are processed.
    /// Performance: SIMD skip-ahead for zero blocks
    /// </remarks>
    public static List<(int X, int Y)[]> FindComponents(Span2D<float> probabilityMap)
    {
        Debug.Assert(probabilityMap.ToArray().Cast<float>().Min() >= 0);
        int height = probabilityMap.Height;
        int width = probabilityMap.Width;

        List<(int X, int Y)[]> components = [];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (probabilityMap[y, x] > 0)
                {
                    var component = ExploreComponent(x, y, probabilityMap, height, width);
                    components.Add(component);
                }
            }
        }

        return components;
    }

    /// <summary>
    /// Explores a single connected component starting from the given seed point using depth-first flood fill.
    /// Marks visited pixels by setting them to 0 in the probability map.
    /// </summary>
    /// <param name="x">Starting X coordinate (seed point)</param>
    /// <param name="y">Starting Y coordinate (seed point)</param>
    /// <param name="probabilityMap">2D probability map to explore (modified during processing)</param>
    /// <param name="height">Height of the probability map</param>
    /// <param name="width">Width of the probability map</param>
    /// <returns>Array of all pixel coordinates belonging to this connected component</returns>
    /// <remarks>Performance: use scanline flood fill; return boundary pixels only</remarks>
    private static (int X, int Y)[] ExploreComponent(int x, int y, Span2D<float> probabilityMap, int height, int width)
    {
        List<(int X, int Y)> component = [];
        Stack<(int X, int Y)> stack = [];

        stack.Push((x, y));

        while (stack.Count > 0)
        {
            (x, y) = stack.Pop();

            if (probabilityMap[y, x] <= 0) continue;

            component.Add((x, y));
            probabilityMap[y, x] = 0;

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;

                    int nx = x + dx;
                    int ny = y + dy;

                    if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                    {
                        if (probabilityMap[ny, nx] > 0)
                        {
                            stack.Push((nx, ny));
                        }
                    }
                }
            }
        }

        return component.ToArray();
    }
}
