namespace Ocr.Algorithms;

public static class MinAreaRectangle
{
    /// <summary>
    /// Computes the minimum area oriented bounding rectangle for a convex hull.
    /// Uses a simple brute force approach suitable for small point sets (&lt;20 points).
    /// </summary>
    /// <param name="convexHull">Convex hull points in counter-clockwise order</param>
    /// <returns>
    /// List of 4 corner points of the oriented rectangle in counter-clockwise order,
    /// or axis-aligned rectangle corners if fewer than 3 points provided.
    /// </returns>
    public static List<(int X, int Y)> Compute(List<(int X, int Y)> convexHull)
    {
        if (convexHull.Count == 0)
            throw new ArgumentException("Convex hull cannot be empty", nameof(convexHull));

        if (convexHull.Count < 3)
        {
            // For degenerate cases, return axis-aligned rectangle
            return GetAxisAlignedCorners(convexHull);
        }

        double minArea = double.MaxValue;
        List<(int X, int Y)> bestRectangle = GetAxisAlignedCorners(convexHull);

        // Try each edge of the convex hull as a potential side of the rectangle
        for (int i = 0; i < convexHull.Count; i++)
        {
            var p1 = convexHull[i];
            var p2 = convexHull[(i + 1) % convexHull.Count];

            // Calculate the oriented rectangle for this edge
            var rectangle = GetOrientedRectangle(convexHull, p1, p2);
            double area = CalculateRectangleArea(rectangle);

            if (area < minArea)
            {
                minArea = area;
                bestRectangle = rectangle;
            }
        }

        return bestRectangle;
    }

    private static List<(int X, int Y)> GetAxisAlignedCorners(List<(int X, int Y)> points)
    {
        var aaRect = AxisAlignedRectangle.Compute(points);
        return new List<(int X, int Y)>
        {
            (aaRect.Left, aaRect.Top),      // Top-left
            (aaRect.Right - 1, aaRect.Top), // Top-right
            (aaRect.Right - 1, aaRect.Bottom - 1), // Bottom-right
            (aaRect.Left, aaRect.Bottom - 1)  // Bottom-left
        };
    }

    private static List<(int X, int Y)> GetOrientedRectangle(List<(int X, int Y)> convexHull, (int X, int Y) p1, (int X, int Y) p2)
    {
        // Vector along the edge
        double edgeX = p2.X - p1.X;
        double edgeY = p2.Y - p1.Y;
        double edgeLength = Math.Sqrt(edgeX * edgeX + edgeY * edgeY);

        if (edgeLength < 1e-9)
        {
            return GetAxisAlignedCorners(convexHull);
        }

        // Unit vectors for the oriented coordinate system
        double ux = edgeX / edgeLength;  // Along edge
        double uy = edgeY / edgeLength;
        double vx = -uy;  // Perpendicular to edge
        double vy = ux;

        // Project all points onto the oriented axes
        double minU = double.MaxValue, maxU = double.MinValue;
        double minV = double.MaxValue, maxV = double.MinValue;

        foreach (var point in convexHull)
        {
            double u = (point.X - p1.X) * ux + (point.Y - p1.Y) * uy;
            double v = (point.X - p1.X) * vx + (point.Y - p1.Y) * vy;

            minU = Math.Min(minU, u);
            maxU = Math.Max(maxU, u);
            minV = Math.Min(minV, v);
            maxV = Math.Max(maxV, v);
        }

        // Convert back to original coordinate system
        var corners = new List<(int X, int Y)>(4);

        // Corner coordinates in oriented system
        var cornerCoords = new[]
        {
            (minU, minV), // Bottom-left in oriented system
            (maxU, minV), // Bottom-right in oriented system
            (maxU, maxV), // Top-right in oriented system
            (minU, maxV)  // Top-left in oriented system
        };

        foreach (var (u, v) in cornerCoords)
        {
            double x = p1.X + u * ux + v * vx;
            double y = p1.Y + u * uy + v * vy;
            corners.Add(((int)Math.Round(x), (int)Math.Round(y)));
        }

        return corners;
    }

    private static double CalculateRectangleArea(List<(int X, int Y)> rectangle)
    {
        if (rectangle.Count != 4)
            return double.MaxValue;

        // Calculate area using cross product of two adjacent sides
        var p0 = rectangle[0];
        var p1 = rectangle[1];
        var p3 = rectangle[3];

        double side1X = p1.X - p0.X;
        double side1Y = p1.Y - p0.Y;
        double side2X = p3.X - p0.X;
        double side2Y = p3.Y - p0.Y;

        double side1Length = Math.Sqrt(side1X * side1X + side1Y * side1Y);
        double side2Length = Math.Sqrt(side2X * side2X + side2Y * side2Y);

        return side1Length * side2Length;
    }
}
