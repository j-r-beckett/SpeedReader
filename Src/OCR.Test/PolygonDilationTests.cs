using OCR.Algorithms;

namespace OCR.Test;

public class PolygonDilationTests
{
    [Fact]
    public void DilatePolygon_EmptyPolygon_ReturnsNull()
    {
        var polygon = Array.Empty<(int X, int Y)>();

        var result = PolygonDilation.DilatePolygon(polygon);

        Assert.Null(result);
    }

    [Fact]
    public void DilatePolygon_SinglePoint_ReturnsNull()
    {
        var polygon = new[] { (5, 3) };

        var result = PolygonDilation.DilatePolygon(polygon);

        Assert.Null(result);
    }

    [Fact]
    public void DilatePolygon_TwoPoints_ReturnsNull()
    {
        var polygon = new[] { (0, 0), (3, 4) };

        var result = PolygonDilation.DilatePolygon(polygon);

        Assert.Null(result);
    }

    [Fact]
    public void DilatePolygon_VerySmallArea_ReturnsNull()
    {
        // Degenerate triangle with area < 9 pixels
        var polygon = new[] { (0, 0), (1, 0), (0, 1) };

        var result = PolygonDilation.DilatePolygon(polygon);

        Assert.Null(result);
    }

    [Fact]
    public void DilatePolygon_MinimumValidTriangle_ReturnsDilatedPolygon()
    {
        // Triangle with area >= 9 pixels: area = 0.5 * base * height = 0.5 * 6 * 3 = 9
        var polygon = new[] { (0, 0), (6, 0), (3, 3) };

        var result = PolygonDilation.DilatePolygon(polygon);

        Assert.NotNull(result);
        Assert.True(result.Length >= 3);

        // Dilated polygon should be larger than original
        var originalBounds = GetBounds(polygon);
        var dilatedBounds = GetBounds(result);

        Assert.True(dilatedBounds.Width >= originalBounds.Width);
        Assert.True(dilatedBounds.Height >= originalBounds.Height);
    }

    [Fact]
    public void DilatePolygon_Square_ReturnsDilatedSquare()
    {
        // 10x10 square (area = 100)
        var polygon = new[] { (0, 0), (10, 0), (10, 10), (0, 10) };

        var result = PolygonDilation.DilatePolygon(polygon);

        Assert.NotNull(result);
        Assert.True(result.Length >= 4);

        // Check that dilation expanded the polygon
        var originalBounds = GetBounds(polygon);
        var dilatedBounds = GetBounds(result);

        Assert.True(dilatedBounds.Width > originalBounds.Width);
        Assert.True(dilatedBounds.Height > originalBounds.Height);

        // Center should be approximately the same
        var originalCenter = GetCenter(polygon);
        var dilatedCenter = GetCenter(result);

        Assert.True(Math.Abs(originalCenter.X - dilatedCenter.X) < 2);
        Assert.True(Math.Abs(originalCenter.Y - dilatedCenter.Y) < 2);
    }

    [Fact]
    public void DilatePolygon_Rectangle_ReturnsDilatedRectangle()
    {
        // 20x5 rectangle (area = 100)
        var polygon = new[] { (0, 0), (20, 0), (20, 5), (0, 5) };

        var result = PolygonDilation.DilatePolygon(polygon);

        Assert.NotNull(result);
        Assert.True(result.Length >= 4);

        // Verify dilation expanded the polygon
        var originalBounds = GetBounds(polygon);
        var dilatedBounds = GetBounds(result);

        Assert.True(dilatedBounds.Width > originalBounds.Width);
        Assert.True(dilatedBounds.Height > originalBounds.Height);
    }

    [Fact]
    public void DilatePolygon_LargeSquare_ProducesProportionalDilation()
    {
        // 100x100 square (area = 10000)
        var polygon = new[] { (0, 0), (100, 0), (100, 100), (0, 100) };

        var result = PolygonDilation.DilatePolygon(polygon);

        Assert.NotNull(result);

        // Calculate expected offset using DBNet formula: D' = A' × r' / L'
        // Area = 10000, Perimeter = 400, r = 1.5
        // Expected offset = 10000 * 1.5 / 400 = 37.5

        var originalBounds = GetBounds(polygon);
        var dilatedBounds = GetBounds(result);

        var expectedIncrease = 37.5 * 2; // Both sides expand
        var actualWidthIncrease = dilatedBounds.Width - originalBounds.Width;
        var actualHeightIncrease = dilatedBounds.Height - originalBounds.Height;

        // Allow some tolerance due to rounding and rounded corners
        Assert.True(Math.Abs(actualWidthIncrease - expectedIncrease) < 10);
        Assert.True(Math.Abs(actualHeightIncrease - expectedIncrease) < 10);
    }

    [Fact]
    public void DilatePolygon_ComplexShape_MaintainsGeneralForm()
    {
        // L-shaped polygon
        var polygon = new[]
        {
            (0, 0), (10, 0), (10, 5), (5, 5), (5, 10), (0, 10)
        };

        var result = PolygonDilation.DilatePolygon(polygon);

        Assert.NotNull(result);
        Assert.True(result.Length >= 6);

        // Verify dilation expanded the polygon
        var originalBounds = GetBounds(polygon);
        var dilatedBounds = GetBounds(result);

        Assert.True(dilatedBounds.Width > originalBounds.Width);
        Assert.True(dilatedBounds.Height > originalBounds.Height);
    }

    [Fact]
    public void DilatePolygon_ZeroPerimeter_ReturnsNull()
    {
        // Degenerate case: all points are the same (zero perimeter)
        var polygon = new[] { (5, 5), (5, 5), (5, 5) };

        var result = PolygonDilation.DilatePolygon(polygon);

        Assert.Null(result);
    }

    [Fact]
    public void DilatePolygons_EmptyArray_ReturnsEmptyArray()
    {
        var polygons = Array.Empty<(int X, int Y)[]>();

        var result = PolygonDilation.DilatePolygons(polygons);

        Assert.Empty(result);
    }

    [Fact]
    public void DilatePolygons_MixedValidAndInvalid_ReturnsOnlyValid()
    {
        var polygons = new[]
        {
            new[] { (0, 0), (1, 0) }, // Invalid: too few points
            new[] { (0, 0), (6, 0), (3, 3) }, // Valid: triangle with area = 9
            new[] { (5, 5), (5, 5), (5, 5) }, // Invalid: zero perimeter
            new[] { (10, 10), (20, 10), (20, 20), (10, 20) } // Valid: square
        };

        var result = PolygonDilation.DilatePolygons(polygons);

        Assert.Equal(2, result.Length);

        // Both results should be non-null and have at least 3 vertices
        foreach (var dilated in result)
        {
            Assert.NotNull(dilated);
            Assert.True(dilated.Length >= 3);
        }
    }

    [Fact]
    public void DilatePolygons_MultipleValidPolygons_ReturnsAllDilated()
    {
        var polygons = new[]
        {
            new[] { (0, 0), (10, 0), (10, 10), (0, 10) }, // Square 1
            new[] { (20, 20), (30, 20), (30, 30), (20, 30) }, // Square 2
            new[] { (0, 20), (8, 20), (4, 26) } // Triangle (area = 24)
        };

        var result = PolygonDilation.DilatePolygons(polygons);

        Assert.Equal(3, result.Length);

        // All results should be valid dilated polygons
        foreach (var dilated in result)
        {
            Assert.NotNull(dilated);
            Assert.True(dilated.Length >= 3);
        }
    }

    [Fact]
    public void DilatePolygon_ConsistentResults_SameInputSameOutput()
    {
        var polygon = new[] { (0, 0), (10, 0), (10, 10), (0, 10) };

        var result1 = PolygonDilation.DilatePolygon(polygon);
        var result2 = PolygonDilation.DilatePolygon(polygon);

        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.Equal(result1.Length, result2.Length);

        for (int i = 0; i < result1.Length; i++)
        {
            Assert.Equal(result1[i], result2[i]);
        }
    }

    [Fact]
    public void DilatePolygon_NegativeCoordinates_WorksCorrectly()
    {
        var polygon = new[] { (-10, -10), (0, -10), (0, 0), (-10, 0) };

        var result = PolygonDilation.DilatePolygon(polygon);

        Assert.NotNull(result);
        Assert.True(result.Length >= 4);

        // Verify dilation expanded the polygon
        var originalBounds = GetBounds(polygon);
        var dilatedBounds = GetBounds(result);

        Assert.True(dilatedBounds.Width > originalBounds.Width);
        Assert.True(dilatedBounds.Height > originalBounds.Height);
    }

    [Fact]
    public void DilatePolygon_LargeCoordinates_WorksCorrectly()
    {
        var polygon = new[] { (1000, 1000), (1100, 1000), (1100, 1100), (1000, 1100) };

        var result = PolygonDilation.DilatePolygon(polygon);

        Assert.NotNull(result);
        Assert.True(result.Length >= 4);

        // Verify coordinates are still in reasonable range
        foreach (var point in result)
        {
            Assert.True(point.X >= 900 && point.X <= 1200);
            Assert.True(point.Y >= 900 && point.Y <= 1200);
        }
    }

    [Fact]
    public void DilatePolygon_VeryThinRectangle_HandlesHighAspectRatio()
    {
        // Very thin rectangle: 100x1 (area = 100, perimeter = 202)
        var polygon = new[] { (0, 0), (100, 0), (100, 1), (0, 1) };

        var result = PolygonDilation.DilatePolygon(polygon);

        Assert.NotNull(result);
        Assert.True(result.Length >= 4);

        // Thin rectangle should still dilate properly
        var originalBounds = GetBounds(polygon);
        var dilatedBounds = GetBounds(result);

        Assert.True(dilatedBounds.Width > originalBounds.Width);
        Assert.True(dilatedBounds.Height > originalBounds.Height);
    }

    // Helper methods for test validation
    private static (int X, int Y, int Width, int Height) GetBounds((int X, int Y)[] polygon)
    {
        var minX = polygon.Min(p => p.X);
        var maxX = polygon.Max(p => p.X);
        var minY = polygon.Min(p => p.Y);
        var maxY = polygon.Max(p => p.Y);

        return (minX, minY, maxX - minX, maxY - minY);
    }

    private static (double X, double Y) GetCenter((int X, int Y)[] polygon)
    {
        return (polygon.Average(p => p.X), polygon.Average(p => p.Y));
    }
}
