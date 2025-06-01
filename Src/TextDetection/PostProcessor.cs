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

    public static void BinarizeProbabilityMap(Span2D<float> probabilityMap)
    {
        int height = probabilityMap.Height;
        int width = probabilityMap.Width;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                probabilityMap[y, x] = probabilityMap[y, x] > 0.2f ? 1.0f : 0.0f;
            }
        }
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

    public static (int X, int Y)[] ConvexHull((int X, int Y)[] points)
    {
        if (points.Length < 3)
        {
            return points.Length < 3 ? [] : points.ToArray();
        }

        var stack = new Stack<(int, int)>();
        var minYPoint = GetStartPoint();
        Array.Sort(points, (p1, p2) => ComparePolarAngle(minYPoint, p1, p2));
        stack.Push(points[0]);  // this is minYPoint, guaranteed to be on the hull
        stack.Push(points[1]);  // not guaranteed to be on the hull, may get popped

        for (int i = 2; i < points.Length; i++)
        {
            var next = points[i];
            var p = stack.Pop();
            while (stack.Count > 0 && CrossProductZ(stack.Peek(), p, next) <= 0)
            {
                p = stack.Pop();  // delete points that create a clockwise turn
            }
            stack.Push(p);
            stack.Push(next);
        }

        var lastPoint = stack.Pop();  // Last point pushed could have been collinear
        if (CrossProductZ(stack.Peek(), lastPoint, minYPoint) > 0)
        {
            stack.Push(lastPoint);  // It wasn't, put it back
        }

        var result = stack.ToArray();
        Array.Reverse(result);
        return result;

        // Returns the point with the smallest y coordinate
        (int X, int Y) GetStartPoint()
        {
            (int bestX, int bestY) = points[0];

            for (int i = 1; i < points.Length; i++)
            {
                if (points[i].Y < bestY || points[i].Y == bestY && points[i].X < bestX)
                {
                    (bestX, bestY) = points[i];
                }
            }

            return (bestX, bestY);
        }

        int CrossProductZ((int X, int Y) a, (int X, int Y) b, (int X, int Y) c)
        {
            return (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        }

        // Return true if the polar angle from anchor -> p1 is less than the polar angle from anchor -> p2.
        // If you swung the x unit vector up and around, return true if you'd hit p1 first.
        int ComparePolarAngle((int X, int Y) anchor, (int X, int Y) p1, (int X, int Y) p2)
        {
            int crossZ = CrossProductZ(anchor, p1, p2);

            if (crossZ < 0) return 1;
            if (crossZ > 0) return -1;

            // Points are collinear, sort by squared Euclidean distance
            (int X, int Y) v1 = (p1.X - anchor.X, p1.Y - anchor.Y);
            (int X, int Y) v2 = (p2.X - anchor.X, p2.Y - anchor.Y);
            int dist1 = v1.X * v1.X + v1.Y * v1.Y;
            int dist2 = v2.X * v2.X + v2.Y * v2.Y;
            return dist1.CompareTo(dist2);
        }
    }
}
