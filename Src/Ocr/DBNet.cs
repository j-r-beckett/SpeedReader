using System.Buffers;
using System.Numerics.Tensors;
using CommunityToolkit.HighPerformance;
using Ocr.Algorithms;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ocr;

public static class DBNet
{
    private const float BinarizationThreshold = 0.2f;

    public static Buffer<float> PreProcess(Image<Rgb24>[] batch)
    {
        if (batch.Length == 0)
        {
            throw new ArgumentException("Batch cannot be empty", nameof(batch));
        }

        var originalDimensions = new (int Width, int Height)[batch.Length];
        for (int i = 0; i < batch.Length; i++)
        {
            originalDimensions[i] = (batch[i].Width, batch[i].Height);
        }

        (int width, int height) = CalculateDimensions(batch);

        var buffer = new Buffer<float>(batch.Length * 3 * height * width, [batch.Length, height, width, 3]);

        for (int i = 0; i < batch.Length; i++)
        {
            var dest = buffer.AsSpan().Slice(i * width * height * 3, width * height * 3);
            Resampling.AspectResizeInto(batch[i], dest, width, height);
        }

        // Convert to NCHW in place and update Shape
        TensorOps.NhwcToNchw(buffer);

        // Normalize each channel using tensor operations
        var tensor = buffer.AsTensor();
        var tensorSpan = tensor.AsTensorSpan();

        float[] means = [123.675f, 116.28f, 103.53f];
        float[] stds = [58.395f, 57.12f, 57.375f];

        for (int channel = 0; channel < 3; channel++)
        {
            // Range over a single color channel
            ReadOnlySpan<NRange> channelRange = [
                NRange.All,                           // All batches
                new (channel, channel + 1),     // One channel
                NRange.All,                           // All heights
                NRange.All                            // All widths
            ];

            var channelSlice = tensorSpan[channelRange];

            // Subtract mean and divide by std in place
            Tensor.Subtract(channelSlice, means[channel], channelSlice);
            Tensor.Divide(channelSlice, stds[channel], channelSlice);
        }

        return buffer;
    }

    internal static List<Rectangle>[] PostProcess(Buffer<float> batch, Image<Rgb24>[] originalBatch)
    {
        int n = (int)batch.Shape[0];
        int height = (int)batch.Shape[1];
        int width = (int)batch.Shape[2];
        int size = height * width;

        List<Rectangle>[] results = new List<Rectangle>[n];

        // Create a copy of the data for score calculation before binarization
        var probabilityData = batch.AsSpan().ToArray();
        
        // Binarize for connected component analysis
        Thresholding.BinarizeInPlace(batch.AsTensor(), BinarizationThreshold);

        for (int i = 0; i < n; i++)
        {
            var probabilityMap = batch.AsSpan().Slice(i * size, size).AsSpan2D(height, width);
            var scoreMap = new Span2D<float>(probabilityData, i * size, height, width, 0);
            var components = ConnectedComponents.FindComponents(probabilityMap);
            List<Rectangle> boundingBoxes = [];
            (int originalWidth, int originalHeight) = (originalBatch[i].Width, originalBatch[i].Height);
            
            // Calculate the actual fitted dimensions (without padding) for this image
            var (fittedWidth, fittedHeight) = CalculateFittedDimensions(originalWidth, originalHeight);

            foreach (var connectedComponent in components)
            {
                // Skip very small components
                if (connectedComponent.Length < 10)
                {
                    continue;
                }
                
                // Extract contour boundary instead of using convex hull
                var contour = ExtractContour(connectedComponent, height, width);
                if (contour.Count < 4)
                {
                    continue;
                }
                
                // Calculate confidence score for this region
                float score = CalculateBoxScore(scoreMap, contour);
                if (score < BinarizationThreshold)
                {
                    continue;
                }
                
                // Apply polygon approximation to reduce vertices
                var approximatedPolygon = ApproximatePolygon(contour);
                if (approximatedPolygon.Count < 4)
                {
                    continue;
                }
                
                // Dilate the polygon
                var dilatedPolygon = Dilation.DilatePolygon(approximatedPolygon);
                if (dilatedPolygon.Count == 0)
                {
                    continue;
                }
                
                // Scale to original coordinates using fitted dimensions
                Scale(dilatedPolygon, originalWidth, originalHeight, fittedWidth, fittedHeight);
                var boundingBox = GetBoundingBox(dilatedPolygon);
                boundingBoxes.Add(boundingBox);
            }

            results[i] = boundingBoxes;
        }

        return results;
    }

    internal static void Scale(List<(int X, int Y)> polygon, int originalWidth, int originalHeight, int modelWidth, int modelHeight)
    {
        float scaleX = (float)originalWidth / modelWidth;
        float scaleY = (float)originalHeight / modelHeight;

        for (int i = 0; i < polygon.Count; i++)
        {
            int originalX = (int)Math.Round(polygon[i].X * scaleX);
            int originalY = (int)Math.Round(polygon[i].Y * scaleY);

            originalX = Math.Clamp(originalX, 0, originalWidth - 1);
            originalY = Math.Clamp(originalY, 0, originalHeight - 1);

            polygon[i] = (originalX, originalY);
        }
    }

    internal static Rectangle GetBoundingBox(List<(int X, int Y)> polygon)
    {
        int maxX = int.MinValue;
        int maxY = int.MinValue;
        int minX = int.MaxValue;
        int minY = int.MaxValue;

        foreach ((int x, int y) in polygon)
        {
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
        }

        return new Rectangle(minX, minY, maxX - minX, maxY - minY);
    }

    internal static (int width, int height) CalculateDimensions(Image<Rgb24>[] batch)
    {
        int maxWidth = -1;
        int maxHeight = -1;

        foreach (var image in batch)
        {
            var (fittedWidth, fittedHeight) = CalculateFittedDimensions(image.Width, image.Height);
            int paddedWidth = (fittedWidth + 31) / 32 * 32;
            int paddedHeight = (fittedHeight + 31) / 32 * 32;
            maxWidth = Math.Max(maxWidth, paddedWidth);
            maxHeight = Math.Max(maxHeight, paddedHeight);
        }

        return (maxWidth, maxHeight);
    }

    private static (int width, int height) CalculateFittedDimensions(int originalWidth, int originalHeight)
    {
        double scale = Math.Min((double)1333 / originalWidth, (double)736 / originalHeight);
        int fittedWidth = (int)Math.Round(originalWidth * scale);
        int fittedHeight = (int)Math.Round(originalHeight * scale);
        return (fittedWidth, fittedHeight);
    }

    private static List<(int X, int Y)> ExtractContour((int X, int Y)[] component, int height, int width)
    {
        // Create a binary map for the component
        var componentMap = new bool[height, width];
        foreach (var point in component)
        {
            componentMap[point.Y, point.X] = true;
        }
        
        // Find a starting point on the boundary
        (int X, int Y)? startPoint = null;
        foreach (var point in component)
        {
            // Check if this point is on the boundary (has at least one non-component neighbor)
            bool isBoundary = false;
            for (int dy = -1; dy <= 1 && !isBoundary; dy++)
            {
                for (int dx = -1; dx <= 1 && !isBoundary; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = point.X + dx;
                    int ny = point.Y + dy;
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height || !componentMap[ny, nx])
                    {
                        isBoundary = true;
                        startPoint = point;
                    }
                }
            }
            if (isBoundary) break;
        }
        
        if (!startPoint.HasValue)
        {
            return [];
        }
        
        // Trace the boundary using Moore neighborhood tracing
        var boundary = new List<(int X, int Y)>();
        var current = startPoint.Value;
        var start = current;
        
        // Direction vectors for 8-connectivity (clockwise from right)
        var directions = new (int dx, int dy)[] 
        {
            (1, 0), (1, 1), (0, 1), (-1, 1), 
            (-1, 0), (-1, -1), (0, -1), (1, -1)
        };
        
        int dir = 0; // Start looking to the right
        int maxIterations = component.Length * 2; // Prevent infinite loops
        int iterations = 0;
        
        do
        {
            boundary.Add(current);
            
            // Find next boundary pixel
            bool found = false;
            for (int i = 0; i < 8; i++)
            {
                int checkDir = (dir + 6 + i) % 8; // Start from behind-left of current direction
                int nx = current.X + directions[checkDir].dx;
                int ny = current.Y + directions[checkDir].dy;
                
                if (nx >= 0 && nx < width && ny >= 0 && ny < height && componentMap[ny, nx])
                {
                    current = (nx, ny);
                    dir = checkDir;
                    found = true;
                    break;
                }
            }
            
            if (!found || ++iterations > maxIterations)
            {
                break;
            }
        }
        while (current != start || boundary.Count < 3);
        
        return boundary;
    }
    
    private static float CalculateBoxScore(Span2D<float> scoreMap, List<(int X, int Y)> polygon)
    {
        if (polygon.Count == 0)
        {
            return 0f;
        }
        
        // Get bounding box of the polygon
        int minX = polygon.Min(p => p.X);
        int minY = polygon.Min(p => p.Y);
        int maxX = polygon.Max(p => p.X);
        int maxY = polygon.Max(p => p.Y);
        
        // Create a mask for the polygon region
        int boxWidth = maxX - minX + 1;
        int boxHeight = maxY - minY + 1;
        var mask = new bool[boxHeight, boxWidth];
        
        // Fill polygon using scanline algorithm
        for (int y = 0; y < boxHeight; y++)
        {
            var intersections = new List<int>();
            int worldY = y + minY;
            
            // Find all intersections with polygon edges
            for (int i = 0; i < polygon.Count; i++)
            {
                var p1 = polygon[i];
                var p2 = polygon[(i + 1) % polygon.Count];
                
                if ((p1.Y <= worldY && p2.Y > worldY) || (p2.Y <= worldY && p1.Y > worldY))
                {
                    // Calculate intersection X coordinate
                    float t = (float)(worldY - p1.Y) / (p2.Y - p1.Y);
                    int intersectX = (int)(p1.X + t * (p2.X - p1.X)) - minX;
                    intersections.Add(intersectX);
                }
            }
            
            // Sort intersections and fill between pairs
            intersections.Sort();
            for (int i = 0; i < intersections.Count - 1; i += 2)
            {
                for (int x = Math.Max(0, intersections[i]); x <= Math.Min(boxWidth - 1, intersections[i + 1]); x++)
                {
                    mask[y, x] = true;
                }
            }
        }
        
        // Calculate mean score within the mask
        float sum = 0f;
        int count = 0;
        
        for (int y = 0; y < boxHeight; y++)
        {
            for (int x = 0; x < boxWidth; x++)
            {
                if (mask[y, x])
                {
                    int worldX = x + minX;
                    int worldY = y + minY;
                    if (worldX >= 0 && worldX < scoreMap.Width && worldY >= 0 && worldY < scoreMap.Height)
                    {
                        sum += scoreMap[worldY, worldX];
                        count++;
                    }
                }
            }
        }
        
        return count > 0 ? sum / count : 0f;
    }
    
    private static List<(int X, int Y)> ApproximatePolygon(List<(int X, int Y)> polygon)
    {
        if (polygon.Count < 3)
        {
            return polygon;
        }
        
        // Calculate perimeter
        double perimeter = 0;
        for (int i = 0; i < polygon.Count; i++)
        {
            var p1 = polygon[i];
            var p2 = polygon[(i + 1) % polygon.Count];
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            perimeter += Math.Sqrt(dx * dx + dy * dy);
        }
        
        // Use Douglas-Peucker algorithm with epsilon = 1% of perimeter
        double epsilon = perimeter * 0.01;
        return DouglasPeucker(polygon, epsilon);
    }
    
    private static List<(int X, int Y)> DouglasPeucker(List<(int X, int Y)> points, double epsilon)
    {
        if (points.Count < 3)
        {
            return points;
        }
        
        // Find the point with maximum distance from the line between first and last point
        double maxDistance = 0;
        int maxIndex = 0;
        
        for (int i = 1; i < points.Count - 1; i++)
        {
            double distance = PerpendicularDistance(points[i], points[0], points[^1]);
            if (distance > maxDistance)
            {
                maxDistance = distance;
                maxIndex = i;
            }
        }
        
        // If max distance is greater than epsilon, recursively simplify
        if (maxDistance > epsilon)
        {
            var leftPart = DouglasPeucker(points.GetRange(0, maxIndex + 1), epsilon);
            var rightPart = DouglasPeucker(points.GetRange(maxIndex, points.Count - maxIndex), epsilon);
            
            // Combine results (remove duplicate point at maxIndex)
            var result = new List<(int X, int Y)>(leftPart);
            result.AddRange(rightPart.Skip(1));
            return result;
        }
        else
        {
            // Return only endpoints
            return [points[0], points[^1]];
        }
    }
    
    private static double PerpendicularDistance((int X, int Y) point, (int X, int Y) lineStart, (int X, int Y) lineEnd)
    {
        double dx = lineEnd.X - lineStart.X;
        double dy = lineEnd.Y - lineStart.Y;
        
        if (dx == 0 && dy == 0)
        {
            // Line start and end are the same point
            dx = point.X - lineStart.X;
            dy = point.Y - lineStart.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
        
        double normalLength = Math.Sqrt(dx * dx + dy * dy);
        return Math.Abs((point.X - lineStart.X) * dy - (point.Y - lineStart.Y) * dx) / normalLength;
    }

}
