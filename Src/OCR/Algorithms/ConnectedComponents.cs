using System.Numerics.Tensors;
using CommunityToolkit.HighPerformance;

namespace OCR.Algorithms;

public static class ConnectedComponents
{
    public static (int X, int Y)[][] FindComponents(Span2D<float> probabilityMap)
    {
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

        return components.ToArray();
    }

    // TODO: replace with scanline flood fill
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
