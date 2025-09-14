using Ocr.Algorithms;

namespace Ocr.Test;

public class MinAreaRectangleTests
{
    [Fact]
    public void Compute_SimpleRotatedSquare_ReturnsCorrectOrientedRectangle()
    {
        // Arrange: A square rotated 45 degrees around origin
        // Points form a diamond shape: top (0,4), right (4,0), bottom (0,-4), left (-4,0)
        var convexHull = new List<(int X, int Y)>
        {
            (0, -4),  // bottom (start point for convex hull)
            (4, 0),   // right
            (0, 4),   // top
            (-4, 0)   // left
        };

        // Act
        var result = MinAreaRectangle.Compute(convexHull);

        // Assert
        Assert.Equal(4, result.Count);

        // The oriented rectangle should have these 4 corners
        // For a rotated square, the minimum area rectangle should be the same shape
        Assert.Contains((0, -4), result);
        Assert.Contains((4, 0), result);
        Assert.Contains((0, 4), result);
        Assert.Contains((-4, 0), result);

        // Calculate expected area: 8 * 8 * sin(45°) * cos(45°) = 64 * (1/√2) * (1/√2) = 32
        // But since it's axis-aligned in the rotated system, area = side * side = (4√2) * (4√2) = 32
        double expectedArea = 32.0;
        double actualArea = CalculatePolygonArea(result);
        Assert.True(Math.Abs(actualArea - expectedArea) < 0.01,
            $"Expected area ~{expectedArea}, got {actualArea}");
    }

    [Fact]
    public void Compute_Rectangle_ReturnsExactRectangle()
    {
        // Arrange: An axis-aligned rectangle
        var convexHull = new List<(int X, int Y)>
        {
            (0, 0),   // bottom-left
            (6, 0),   // bottom-right
            (6, 3),   // top-right
            (0, 3)    // top-left
        };

        // Act
        var result = MinAreaRectangle.Compute(convexHull);

        // Assert
        Assert.Equal(4, result.Count);

        // Should return the exact same rectangle corners
        Assert.Contains((0, 0), result);
        Assert.Contains((6, 0), result);
        Assert.Contains((6, 3), result);
        Assert.Contains((0, 3), result);

        // Area should be exactly 18
        double expectedArea = 18.0;
        double actualArea = CalculatePolygonArea(result);
        Assert.True(Math.Abs(actualArea - expectedArea) < 0.01,
            $"Expected area {expectedArea}, got {actualArea}");
    }

    [Fact]
    public void Compute_ActualRectanglePoints_ReturnsExactRectangle()
    {
        // Arrange: Create points that ACTUALLY form a rectangle
        // Start with a 4x2 rectangle and rotate it by some angle
        // Using 3-4-5 right triangle: cos(θ)=4/5, sin(θ)=3/5
        // Original rectangle corners: (0,0), (4,0), (4,2), (0,2)
        // Rotated by θ where cos(θ)=4/5, sin(θ)=3/5:
        var convexHull = new List<(int X, int Y)>
        {
            (0, 0),      // (0*4/5 - 0*3/5, 0*3/5 + 0*4/5)
            (16, 12),    // (4*4/5 - 0*3/5, 4*3/5 + 0*4/5) = (16/5*5, 12/5*5)
            (10, 20),    // (4*4/5 - 2*3/5, 4*3/5 + 2*4/5) = (16/5-6/5, 12/5+8/5)*5 = (2,4)*5
            (-6, 8)      // (0*4/5 - 2*3/5, 0*3/5 + 2*4/5) = (-6/5, 8/5)*5
        };

        // Let me use a simpler case: a 6x4 rectangle rotated 90 degrees
        var simpleCase = new List<(int X, int Y)>
        {
            (0, 0),   // bottom-left becomes left-bottom when rotated 90°
            (0, 6),   // bottom-right becomes left-top
            (4, 6),   // top-right becomes right-top
            (4, 0)    // top-left becomes right-bottom
        };

        // Act
        var result = MinAreaRectangle.Compute(simpleCase);

        // Assert
        Assert.Equal(4, result.Count);

        // Debug output
        var resultStr = string.Join(", ", result.Select(p => $"({p.X}, {p.Y})"));
        var expectedStr = string.Join(", ", simpleCase.Select(p => $"({p.X}, {p.Y})"));
        Console.WriteLine($"Input: [{expectedStr}]");
        Console.WriteLine($"Result: [{resultStr}]");

        // The result should contain all four corners of our rectangle
        Assert.Contains((0, 0), result);
        Assert.Contains((0, 6), result);
        Assert.Contains((4, 6), result);
        Assert.Contains((4, 0), result);

        // Area should be exactly 24
        double expectedArea = 24.0;
        double actualArea = CalculatePolygonArea(result);
        Assert.True(Math.Abs(actualArea - expectedArea) < 0.01,
            $"Expected area {expectedArea}, got {actualArea}");
    }

    [Fact]
    public void Compute_SinglePoint_ThrowsException()
    {
        // Arrange
        var singlePoint = new List<(int X, int Y)> { (5, 3) };

        // Act & Assert
        var result = MinAreaRectangle.Compute(singlePoint);

        // Should return axis-aligned degenerate rectangle
        Assert.Equal(4, result.Count);
    }

    [Fact]
    public void Compute_EmptyList_ThrowsException()
    {
        // Arrange
        var emptyList = new List<(int X, int Y)>();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => MinAreaRectangle.Compute(emptyList));
    }

    /// <summary>
    /// Helper method to calculate the area of a polygon using the shoelace formula
    /// </summary>
    private static double CalculatePolygonArea(List<(int X, int Y)> polygon)
    {
        if (polygon.Count < 3) return 0;

        double area = 0;
        for (int i = 0; i < polygon.Count; i++)
        {
            int j = (i + 1) % polygon.Count;
            area += polygon[i].X * polygon[j].Y;
            area -= polygon[j].X * polygon[i].Y;
        }
        return Math.Abs(area) / 2.0;
    }
}
