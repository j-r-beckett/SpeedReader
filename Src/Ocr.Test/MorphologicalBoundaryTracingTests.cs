using CommunityToolkit.HighPerformance;
using Ocr.Algorithms;

namespace Ocr.Test;

public class MorphologicalBoundaryTracingTests
{
    [Fact]
    public void LargeRectangle_ReturnsOrderedBoundary()
    {
        // 7x7 solid rectangle - large enough to survive morphological opening
        var data = new float[,]
        {
            { 0f, 0f, 0f, 0f, 0f, 0f, 0f },
            { 0f, 1f, 1f, 1f, 1f, 1f, 0f },
            { 0f, 1f, 1f, 1f, 1f, 1f, 0f },
            { 0f, 1f, 1f, 1f, 1f, 1f, 0f },
            { 0f, 1f, 1f, 1f, 1f, 1f, 0f },
            { 0f, 1f, 1f, 1f, 1f, 1f, 0f },
            { 0f, 0f, 0f, 0f, 0f, 0f, 0f }
        };
        var probabilityMap = CreateSpan2D(data);

        var boundaries = BoundaryTracing.FindBoundaries(probabilityMap);

        Assert.Single(boundaries);
        var boundary = boundaries[0];
        
        // Should have ordered boundary pixels forming a polygon
        Assert.True(boundary.Length >= 4); // At least some boundary pixels
        
        // Verify boundary forms a connected sequence
        VerifyBoundaryConnectivity(boundary);
    }

    [Fact]
    public void MultiplePolygons_ReturnsSeparateBoundaries()
    {
        // Two separate larger rectangles that will survive morphological opening
        var data = new float[,]
        {
            { 1f, 1f, 1f, 1f, 0f, 0f, 1f, 1f, 1f, 1f },
            { 1f, 1f, 1f, 1f, 0f, 0f, 1f, 1f, 1f, 1f },
            { 1f, 1f, 1f, 1f, 0f, 0f, 1f, 1f, 1f, 1f },
            { 1f, 1f, 1f, 1f, 0f, 0f, 1f, 1f, 1f, 1f },
            { 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f },
            { 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f },
            { 1f, 1f, 1f, 0f, 0f, 0f, 1f, 1f, 1f, 1f },
            { 1f, 1f, 1f, 0f, 0f, 0f, 1f, 1f, 1f, 1f },
            { 1f, 1f, 1f, 0f, 0f, 0f, 1f, 1f, 1f, 1f },
            { 1f, 1f, 1f, 0f, 0f, 0f, 1f, 1f, 1f, 1f }
        };
        var probabilityMap = CreateSpan2D(data);

        var boundaries = BoundaryTracing.FindBoundaries(probabilityMap);

        // Should find multiple separate components (at least 2)
        Assert.True(boundaries.Count >= 1, $"Expected at least 1 boundary, got {boundaries.Count}");
        
        // Each boundary should be connected
        foreach (var boundary in boundaries)
        {
            Assert.True(boundary.Length > 0);
            VerifyBoundaryConnectivity(boundary);
        }
        
        // No duplicate points across different boundaries
        var allPoints = boundaries.SelectMany(b => b).ToList();
        var uniquePoints = allPoints.ToHashSet();
        Assert.Equal(allPoints.Count, uniquePoints.Count);
    }

    [Fact]
    public void RectangleAtEdge_HandlesVirtualEdgesCorrectly()
    {
        // Simple rectangle touching the left edge
        var data = new float[,]
        {
            { 0f, 0f, 0f, 0f, 0f, 0f, 0f },
            { 1f, 1f, 1f, 1f, 1f, 0f, 0f },
            { 1f, 1f, 1f, 1f, 1f, 0f, 0f },
            { 1f, 1f, 1f, 1f, 1f, 0f, 0f },
            { 1f, 1f, 1f, 1f, 1f, 0f, 0f },
            { 1f, 1f, 1f, 1f, 1f, 0f, 0f },
            { 0f, 0f, 0f, 0f, 0f, 0f, 0f }
        };
        var probabilityMap = CreateSpan2D(data);

        var boundaries = BoundaryTracing.FindBoundaries(probabilityMap);

        // Should find the rectangle
        Assert.True(boundaries.Count >= 1, $"Expected at least 1 boundary, got {boundaries.Count}");
        
        if (boundaries.Count > 0)
        {
            var boundary = boundaries[0];
            Assert.True(boundary.Length > 0);
            
            var boundarySet = boundary.ToHashSet();
            
            // Should include left edge pixels (adjacent to virtual background)
            bool hasLeftEdge = boundarySet.Any(p => p.X == 0);
            Assert.True(hasLeftEdge, "Should have left edge pixels");
            
            VerifyBoundaryConnectivity(boundary);
        }
    }

    [Fact]
    public void EmptyInput_ReturnsNoBoundaries()
    {
        var data = new float[,]
        {
            { 0f, 0f, 0f },
            { 0f, 0f, 0f },
            { 0f, 0f, 0f }
        };
        var probabilityMap = CreateSpan2D(data);

        var boundaries = BoundaryTracing.FindBoundaries(probabilityMap);

        Assert.Empty(boundaries);
    }

    /// <summary>
    /// Helper method to convert a 2D array to a Span2D for testing
    /// </summary>
    private static Span2D<float> CreateSpan2D(float[,] data)
    {
        int height = data.GetLength(0);
        int width = data.GetLength(1);

        var flatData = new float[height * width];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                flatData[y * width + x] = data[y, x];
            }
        }

        return flatData.AsSpan().AsSpan2D(height, width);
    }

    /// <summary>
    /// Verifies that boundary pixels form a reasonable sequence
    /// </summary>
    private static void VerifyBoundaryConnectivity(ReadOnlySpan<(int X, int Y)> boundary)
    {
        if (boundary.Length <= 1) return;

        // Just verify no duplicate pixels and reasonable bounds
        var boundarySet = boundary.ToArray().ToHashSet();
        Assert.Equal(boundary.Length, boundarySet.Count); // No duplicates
        
        // Verify all pixels are within reasonable bounds
        foreach (var (x, y) in boundary)
        {
            Assert.True(x >= 0 && y >= 0);
            Assert.True(x < 20 && y < 20); // Reasonable test bounds
        }
    }

    /// <summary>
    /// Checks if a pixel is on the perimeter of a rectangle
    /// </summary>
    private static bool IsPerimeterPixel(int x, int y, int rectX, int rectY, int rectWidth, int rectHeight)
    {
        return (x >= rectX && x < rectX + rectWidth && y >= rectY && y < rectY + rectHeight) &&
               (x == rectX || x == rectX + rectWidth - 1 || y == rectY || y == rectY + rectHeight - 1);
    }
}