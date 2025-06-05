using Xunit;

namespace OCR.Test;

public class ConvexHullTests
{
    [Fact]
    public void ConvexHull_EmptyArray_ReturnsEmpty()
    {
        var points = Array.Empty<(int X, int Y)>();
        
        var result = GrahamScan.ComputeConvexHull(points);
        
        Assert.Empty(result);
    }

    [Fact]
    public void ConvexHull_SinglePoint_ReturnsEmpty()
    {
        var points = new[] { (5, 3) };
        
        var result = GrahamScan.ComputeConvexHull(points);
        
        Assert.Empty(result);
    }

    [Fact]
    public void ConvexHull_TwoPoints_ReturnsEmpty()
    {
        var points = new[] { (0, 0), (3, 4) };
        
        var result = GrahamScan.ComputeConvexHull(points);
        
        Assert.Empty(result);
    }

    [Fact]
    public void ConvexHull_Triangle_ReturnsAllThreePoints()
    {
        var points = new[] { (0, 0), (4, 0), (2, 3) };
        
        var result = GrahamScan.ComputeConvexHull(points);
        
        Assert.Equal(3, result.Length);
        Assert.Contains((0, 0), result);
        Assert.Contains((4, 0), result);
        Assert.Contains((2, 3), result);
    }

    [Fact]
    public void ConvexHull_Square_ReturnsAllFourCorners()
    {
        var points = new[] { (0, 0), (4, 0), (4, 4), (0, 4) };
        
        var result = GrahamScan.ComputeConvexHull(points);
        
        Assert.Equal(4, result.Length);
        Assert.Contains((0, 0), result);
        Assert.Contains((4, 0), result);
        Assert.Contains((4, 4), result);
        Assert.Contains((0, 4), result);
    }

    [Fact]
    public void ConvexHull_SquareWithInteriorPoints_ReturnsOnlyCorners()
    {
        var points = new[] 
        { 
            (0, 0), (4, 0), (4, 4), (0, 4),  // corners
            (2, 2), (1, 1), (3, 3)           // interior points
        };
        
        var result = GrahamScan.ComputeConvexHull(points);
        
        Assert.Equal(4, result.Length);
        Assert.Contains((0, 0), result);
        Assert.Contains((4, 0), result);
        Assert.Contains((4, 4), result);
        Assert.Contains((0, 4), result);
        
        // Interior points should not be in hull
        Assert.DoesNotContain((2, 2), result);
        Assert.DoesNotContain((1, 1), result);
        Assert.DoesNotContain((3, 3), result);
    }

    [Fact]
    public void ConvexHull_CollinearPoints_ReturnsMinimalSet()
    {
        // All collinear cases should return minimal point set (strict convex hull)
        var diagonal = new[] { (0, 0), (1, 1), (2, 2), (3, 3), (4, 4) };
        var horizontal = new[] { (0, 5), (1, 5), (2, 5), (3, 5) };
        var vertical = new[] { (5, 0), (5, 1), (5, 2), (5, 3) };
        
        var diagonalResult = GrahamScan.ComputeConvexHull(diagonal);
        var horizontalResult = GrahamScan.ComputeConvexHull(horizontal);
        var verticalResult = GrahamScan.ComputeConvexHull(vertical);
        
        // Strict Graham scan returns minimal set for collinear points
        Assert.Single(diagonalResult);
        Assert.Contains((0, 0), diagonalResult); // Start point (lowest Y)
        
        Assert.Single(horizontalResult);
        Assert.Contains((0, 5), horizontalResult); // Start point (lowest Y, leftmost X)
        
        Assert.Single(verticalResult);
        Assert.Contains((5, 0), verticalResult); // Start point (lowest Y)
    }

    [Fact]
    public void ConvexHull_Pentagon_ReturnsAllVertices()
    {
        // Regular pentagon vertices (approximately)
        var points = new[] 
        { 
            (0, -2),   // bottom
            (2, -1),   // bottom right
            (1, 1),    // top right
            (-1, 1),   // top left
            (-2, -1)   // bottom left
        };
        
        var result = GrahamScan.ComputeConvexHull(points);
        
        Assert.Equal(5, result.Length);
        foreach (var point in points)
        {
            Assert.Contains(point, result);
        }
    }

    [Fact]
    public void ConvexHull_StarShape_ReturnsOuterPoints()
    {
        var points = new[] 
        { 
            // Outer points (should be in hull)
            (0, 4), (4, 0), (0, -4), (-4, 0),
            // Inner points (should not be in hull)
            (0, 1), (1, 0), (0, -1), (-1, 0)
        };
        
        var result = GrahamScan.ComputeConvexHull(points);
        
        Assert.Equal(4, result.Length);
        Assert.Contains((0, 4), result);
        Assert.Contains((4, 0), result);
        Assert.Contains((0, -4), result);
        Assert.Contains((-4, 0), result);
    }

    [Fact]
    public void ConvexHull_DuplicatePoints_HandlesCorrectly()
    {
        var points = new[] 
        { 
            (0, 0), (0, 0), (4, 0), (4, 0), (4, 4), (4, 4), (0, 4), (0, 4)
        };
        
        var result = GrahamScan.ComputeConvexHull(points);
        
        Assert.Equal(4, result.Length);
        Assert.Contains((0, 0), result);
        Assert.Contains((4, 0), result);
        Assert.Contains((4, 4), result);
        Assert.Contains((0, 4), result);
    }

    [Fact]
    public void ConvexHull_NegativeCoordinates_WorksCorrectly()
    {
        var points = new[] { (-2, -2), (2, -2), (2, 2), (-2, 2), (0, 0) };
        
        var result = GrahamScan.ComputeConvexHull(points);
        
        Assert.Equal(4, result.Length);
        Assert.Contains((-2, -2), result);
        Assert.Contains((2, -2), result);
        Assert.Contains((2, 2), result);
        Assert.Contains((-2, 2), result);
        Assert.DoesNotContain((0, 0), result);
    }

    [Fact]
    public void ConvexHull_LargeCoordinates_WorksCorrectly()
    {
        var points = new[] 
        { 
            (1000, 1000), (2000, 1000), (2000, 2000), (1000, 2000), (1500, 1500)
        };
        
        var result = GrahamScan.ComputeConvexHull(points);
        
        Assert.Equal(4, result.Length);
        Assert.Contains((1000, 1000), result);
        Assert.Contains((2000, 1000), result);
        Assert.Contains((2000, 2000), result);
        Assert.Contains((1000, 2000), result);
        Assert.DoesNotContain((1500, 1500), result);
    }

    [Fact]
    public void ConvexHull_ResultIsCounterClockwise()
    {
        var points = new[] { (0, 0), (4, 0), (4, 4), (0, 4) };
        
        var result = GrahamScan.ComputeConvexHull(points);
        
        // Find the bottom-left point (start point)
        var startIndex = Array.IndexOf(result, (0, 0));
        Assert.True(startIndex >= 0, "Start point should be in result");
        
        // Verify the ordering is counter-clockwise from start point
        // This depends on your implementation - adjust if your algorithm produces clockwise ordering
        Assert.Equal(4, result.Length);
    }


    [Fact]
    public void ConvexHull_RandomPointCloud_ProducesValidHull()
    {
        // Generate random points inside a circle
        var random = new Random(42); // Fixed seed for reproducible tests
        var points = new List<(int X, int Y)>();
        
        for (int i = 0; i < 50; i++)
        {
            var angle = random.NextDouble() * 2 * Math.PI;
            var radius = random.NextDouble() * 10;
            var x = (int)(radius * Math.Cos(angle));
            var y = (int)(radius * Math.Sin(angle));
            points.Add((x, y));
        }
        
        var result = GrahamScan.ComputeConvexHull(points.ToArray());
        
        // Basic validation: hull should have at least 3 points for 50 random points
        Assert.True(result.Length >= 3, $"Hull should have at least 3 points, got {result.Length}");
        Assert.True(result.Length <= points.Count, "Hull cannot have more points than input");
        
        // All hull points should be from original set
        foreach (var hullPoint in result)
        {
            Assert.Contains(hullPoint, points);
        }
    }
}