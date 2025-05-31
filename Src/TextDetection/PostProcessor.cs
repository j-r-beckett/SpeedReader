using CommunityToolkit.HighPerformance;
using Microsoft.ML.OnnxRuntime;

namespace TextDetection;

public class PostProcessor
{
    public static float[][,] PostProcess(OrtValue tensor)
    {
        var outputSpan = tensor.GetTensorMutableDataAsSpan<float>();
        var shape = tensor.GetTensorTypeAndShape().Shape;

        // Output shape is [batch_size, height, width]
        int batchSize = (int)shape[0];
        int height = (int)shape[1];
        int width = (int)shape[2];

        var results = new float[batchSize][,];
        int imageSize = height * width;

        for (int batchIndex = 0; batchIndex < batchSize; batchIndex++)
        {
            var probabilityMap = new float[height, width];
            int batchOffset = batchIndex * imageSize;

            for (int h = 0; h < height; h++)
            {
                for (int w = 0; w < width; w++)
                {
                    probabilityMap[h, w] = outputSpan[batchOffset + h * width + w];
                }
            }

            results[batchIndex] = probabilityMap;
        }

        return results;
    }

    public static bool[,] BinarizeProbabilityMap(float[,] probabilityMap)
    {
        int height = probabilityMap.GetLength(0);
        int width = probabilityMap.GetLength(1);
        var binaryMap = new bool[height, width];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                binaryMap[y, x] = probabilityMap[y, x] > 0.2f;
            }
        }

        return binaryMap;
    }

    public static (int X, int Y)[][] ConnectedComponents(Span2D<float> data)
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

    // Push starting point -> while stack not empty -> pop point -> if valid, add to component and push its valid neighbors
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
