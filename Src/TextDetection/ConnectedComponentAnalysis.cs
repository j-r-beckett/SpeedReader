using CommunityToolkit.HighPerformance;

namespace TextDetection;

public static class ConnectedComponentAnalysis
{
    public static (int X, int Y)[][] FindComponents(Span2D<float> data)
    {
        List<(int X, int Y)[]> components = [];

        for (int y = 0; y < data.Height; y++)
        {
            for (int x = 0; x < data.Width; x++)
            {
                if (data[y, x] > 0)
                {
                    var component = ExploreComponent(x, y, data);
                    components.Add(component);
                }
            }
        }

        return components.ToArray();
    }

    // TODO: replace with scanline flood fill
    private static (int X, int Y)[] ExploreComponent(int x, int y, Span2D<float> data)
    {
        List<(int X, int Y)> component = [];
        Stack<(int X, int Y)> stack = [];

        stack.Push((x, y));

        while (stack.Count > 0)
        {
            (x, y) = stack.Pop();

            if (data[y, x] <= 0) continue;

            component.Add((x, y));
            data[y, x] = 0;

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;

                    int nx = x + dx;
                    int ny = y + dy;

                    if (nx >= 0 && nx < data.Width && ny >= 0 && ny < data.Height && data[ny, nx] > 0)
                    {
                        stack.Push((nx, ny));
                    }
                }
            }
        }

        return component.ToArray();
    }
}
