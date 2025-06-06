using Ocr.Algorithms;

namespace Ocr.Test;

public class ConvexHullTests
{
    [Theory]
    [InlineData(new int[0], new int[0])]  // Empty array
    [InlineData(new [] { 5 }, new [] { 3 })]  // Single point
    [InlineData(new [] { 0, 3 }, new [] { 0, 4 })]  // Two points
    public void ConvexHull_FewerThanThreePoints_ReturnsEmpty(int[] xCoords, int[] yCoords)
    {
        // Arrange
        var points = xCoords.Zip(yCoords, (x, y) => (x, y)).ToArray();

        // Act
        var result = ConvexHull.GrahamScan(points);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ConvexHull_Triangle_ReturnsAllThreePoints()
    {
        // Arrange
        var points = new[] { (0, 0), (4, 0), (2, 3) };

        // Act
        var result = ConvexHull.GrahamScan(points);

        // Assert
        Assert.Equal(3, result.Length);
        Assert.Contains((0, 0), result);
        Assert.Contains((4, 0), result);
        Assert.Contains((2, 3), result);
    }

    [Fact]
    public void ConvexHull_Square_ReturnsAllFourCorners()
    {
        // Arrange
        var points = new[] { (0, 0), (4, 0), (4, 4), (0, 4) };

        // Act
        var result = ConvexHull.GrahamScan(points);

        // Assert
        Assert.Equal(4, result.Length);
        Assert.Contains((0, 0), result);
        Assert.Contains((4, 0), result);
        Assert.Contains((4, 4), result);
        Assert.Contains((0, 4), result);
    }

    [Fact]
    public void ConvexHull_SquareWithInteriorPoints_ReturnsOnlyCorners()
    {
        // Arrange
        var points = new[]
        {
            (0, 0), (4, 0), (4, 4), (0, 4),  // corners
            (2, 2), (1, 1), (3, 3)           // interior points
        };

        // Act
        var result = ConvexHull.GrahamScan(points);

        // Assert
        Assert.Equal(4, result.Length);
        Assert.Contains((0, 0), result);
        Assert.Contains((4, 0), result);
        Assert.Contains((4, 4), result);
        Assert.Contains((0, 4), result);
    }

    [Fact]
    public void ConvexHull_CollinearPoints_ReturnsMinimalSet()
    {
        // Arrange
        // All collinear cases should return minimal point set (strict convex hull)
        // For us, that means the point with the smallest (y, x)
        var diagonal = new[] { (8, 8), (-2, -2), (4, 4), (0, 0), (6, 6) };
        var horizontal = new[] { (7, 3), (-1, 3), (2, 3), (0, 3) };
        var vertical = new[] { (-3, 9), (-3, 1), (-3, 5), (-3, 3) };

        // Act
        var diagonalResult = ConvexHull.GrahamScan(diagonal);
        var horizontalResult = ConvexHull.GrahamScan(horizontal);
        var verticalResult = ConvexHull.GrahamScan(vertical);

        // Assert
        Assert.Single(diagonalResult);
        Assert.Contains((-2, -2), diagonalResult); // Start point (lowest Y)

        Assert.Single(horizontalResult);
        Assert.Contains((-1, 3), horizontalResult); // Start point (lowest Y, leftmost X)

        Assert.Single(verticalResult);
        Assert.Contains((-3, 1), verticalResult); // Start point (lowest Y)
    }

    [Fact]
    public void ConvexHull_Pentagon_ReturnsAllVertices()
    {
        // Arrange
        // Regular pentagon vertices (approximately)
        var points = new[]
        {
            (0, -2),   // bottom
            (2, -1),   // bottom right
            (1, 1),    // top right
            (-1, 1),   // top left
            (-2, -1)   // bottom left
        };

        // Act
        var result = ConvexHull.GrahamScan(points);

        // Assert
        Assert.Equal(5, result.Length);
        foreach (var point in points)
        {
            Assert.Contains(point, result);
        }
    }

    [Fact]
    public void ConvexHull_StarShape_ReturnsOuterPoints()
    {
        // Arrange
        var points = new[]
        {
            // Outer points (should be in hull)
            (0, 4), (4, 0), (0, -4), (-4, 0),
            // Inner points (should not be in hull)
            (0, 1), (1, 0), (0, -1), (-1, 0)
        };

        // Act
        var result = ConvexHull.GrahamScan(points);

        // Assert
        Assert.Equal(4, result.Length);
        Assert.Contains((0, 4), result);
        Assert.Contains((4, 0), result);
        Assert.Contains((0, -4), result);
        Assert.Contains((-4, 0), result);
    }

    [Fact]
    public void ConvexHull_DuplicatePoints_HandlesCorrectly()
    {
        // Arrange
        var points = new[]
        {
            (0, 0), (0, 0), (4, 0), (4, 0), (4, 4), (4, 4), (0, 4), (0, 4)
        };

        // Act
        var result = ConvexHull.GrahamScan(points);

        // Assert
        Assert.Equal(4, result.Length);
        Assert.Contains((0, 0), result);
        Assert.Contains((4, 0), result);
        Assert.Contains((4, 4), result);
        Assert.Contains((0, 4), result);
    }

    [Fact]
    public void ConvexHull_NegativeCoordinates_WorksCorrectly()
    {
        // Arrange
        var points = new[] { (-2, 2), (0, 0), (-2, -2), (2, 2), (2, -2) };

        // Act
        var result = ConvexHull.GrahamScan(points);

        // Assert
        Assert.Equal(4, result.Length);
        Assert.Contains((-2, -2), result);
        Assert.Contains((2, -2), result);
        Assert.Contains((2, 2), result);
        Assert.Contains((-2, 2), result);
    }

    [Fact]
    public void ConvexHull_LargeCoordinates_WorksCorrectly()
    {
        // Arrange
        var points = new[]
        {
            (1000, 1000), (2000, 1000), (2000, 2000), (1000, 2000), (1500, 1500)
        };

        // Act
        var result = ConvexHull.GrahamScan(points);

        // Assert
        Assert.Equal(4, result.Length);
        Assert.Contains((1000, 1000), result);
        Assert.Contains((2000, 1000), result);
        Assert.Contains((2000, 2000), result);
        Assert.Contains((1000, 2000), result);
    }

    [Fact]
    public void ConvexHull_ResultIsCounterClockwise()
    {
        // Arrange
        var points = new[] { (0, 0), (4, 0), (4, 4), (0, 4) };

        // Act
        var result = ConvexHull.GrahamScan(points);

        // Assert
        Assert.Equal(4, result.Length);

        // Verify counter-clockwise ordering by checking cross products
        for (int i = 0; i < result.Length; i++)
        {
            var current = result[i];
            var next = result[(i + 1) % result.Length];
            var afterNext = result[(i + 2) % result.Length];
            
            // Cross product should be positive for counter-clockwise turns
            var crossProduct = (next.X - current.X) * (afterNext.Y - current.Y) - 
                              (next.Y - current.Y) * (afterNext.X - current.X);
            
            Assert.True(crossProduct > 0, 
                $"Points {current}, {next}, {afterNext} should form counter-clockwise turn, got cross product {crossProduct}");
        }
    }


    [Fact]
    public void ConvexHull_RandomPointCloud_ProducesValidHull()
    {
        // Arrange
        // Generate random points inside a circle
        var random = new Random(0);
        var points = new List<(int X, int Y)>();

        for (int i = 0; i < 500; i++)
        {
            double angle = random.NextDouble() * 2 * Math.PI;
            double radius = random.NextDouble() * 10;
            int x = (int)(radius * Math.Cos(angle));
            int y = (int)(radius * Math.Sin(angle));
            points.Add((x, y));
        }

        // Act
        var result = ConvexHull.GrahamScan(points.ToArray());

        // Assert
        // Basic validation
        Assert.True(result.Length >= 3, $"Hull should have at least 3 points, got {result.Length}");
        Assert.True(result.Length <= points.Count, "Hull cannot have more points than input");

        // All hull points should be from original set
        foreach (var hullPoint in result)
        {
            Assert.Contains(hullPoint, points);
        }
    }
}
