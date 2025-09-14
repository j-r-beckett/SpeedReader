using SixLabors.ImageSharp;

namespace Ocr.Algorithms;

/// <summary>
/// Utilities for computing various bounding shapes around polygon points.
/// </summary>
public static class BoundingRectangles
{
    /// <summary>
    /// Computes the axis-aligned bounding rectangle for a set of polygon points.
    /// Uses the same logic as the original DBNet.GetBoundingBox implementation.
    /// </summary>
    /// <param name="polygon">Polygon points to compute bounding rectangle for</param>
    /// <returns>The smallest axis-aligned rectangle that contains all points</returns>
    /// <exception cref="ArgumentException">Thrown when polygon is null or empty</exception>
    public static Rectangle ComputeAxisAlignedRectangle(List<(int X, int Y)> polygon)
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

    /// <summary>
    /// Computes the oriented bounding rectangle for a set of polygon points.
    /// The oriented rectangle is the smallest rectangle (not necessarily axis-aligned)
    /// that contains all points.
    /// </summary>
    /// <param name="polygon">Polygon points to compute oriented bounding rectangle for</param>
    /// <returns>Four corner points of the oriented rectangle in order: top-left, top-right, bottom-right, bottom-left</returns>
    /// <exception cref="ArgumentException">Thrown when polygon is null or empty</exception>
    public static List<(int X, int Y)> ComputeOrientedRectangle(List<(int X, int Y)> polygon)
    {
        if (polygon == null || polygon.Count == 0)
            throw new ArgumentException("Polygon cannot be null or empty", nameof(polygon));

        // TODO: Implement oriented rectangle computation
        // For now, return axis-aligned rectangle corners as placeholder
        var aaRect = ComputeAxisAlignedRectangle(polygon);
        return new List<(int X, int Y)>
        {
            (aaRect.Left, aaRect.Top),          // Top-left
            (aaRect.Right - 1, aaRect.Top),     // Top-right
            (aaRect.Right - 1, aaRect.Bottom - 1), // Bottom-right
            (aaRect.Left, aaRect.Bottom - 1)    // Bottom-left
        };
    }
}
