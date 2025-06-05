using System.Numerics.Tensors;

namespace TextDetection.Algorithms;

public static class ConnectedComponents
{
    public static (int X, int Y)[][] FindComponents(TensorSpan<float> batchSlice)
    {
        if (batchSlice.Rank != 3)
        {
            throw new ArgumentException($"Expected 3D [1,H,W] tensor, got {batchSlice.Rank}D tensor");
        }

        int height = (int)batchSlice.Lengths[1];
        int width = (int)batchSlice.Lengths[2];
        
        List<(int X, int Y)[]> components = [];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                ReadOnlySpan<nint> indices = [0, y, x];
                if (batchSlice[indices] > 0)
                {
                    var component = ExploreComponent(x, y, batchSlice, height, width);
                    components.Add(component);
                }
            }
        }

        return components.ToArray();
    }

    // TODO: replace with scanline flood fill
    private static (int X, int Y)[] ExploreComponent(int x, int y, TensorSpan<float> batchSlice, int height, int width)
    {
        List<(int X, int Y)> component = [];
        Stack<(int X, int Y)> stack = [];

        stack.Push((x, y));

        while (stack.Count > 0)
        {
            (x, y) = stack.Pop();

            ReadOnlySpan<nint> indices = [0, y, x];
            if (batchSlice[indices] <= 0) continue;

            component.Add((x, y));
            batchSlice[indices] = 0;

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;

                    int nx = x + dx;
                    int ny = y + dy;

                    if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                    {
                        ReadOnlySpan<nint> neighborIndices = [0, ny, nx];
                        if (batchSlice[neighborIndices] > 0)
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