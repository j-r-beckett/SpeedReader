using SixLabors.ImageSharp;

namespace Ocr.Algorithms;

public static class AxisAlignedRectangle
{
    /// <summary>
    /// Calculates the axis-aligned bounding rectangle for a polygon.
    /// </summary>
    /// <param name="polygon">List of polygon vertices</param>
    /// <returns>The smallest axis-aligned rectangle containing all points</returns>
    public static Rectangle Compute(List<(int X, int Y)> polygon)
    {
        if (polygon == null || polygon.Count == 0)
            throw new ArgumentException("Polygon cannot be null or empty", nameof(polygon));

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

        return new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }
}
