using Microsoft.Extensions.Logging;
using Ocr.Algorithms;
using SixLabors.ImageSharp;
using TestUtils;
using Xunit.Abstractions;

namespace Ocr.Test.Algorithms;

public class OrientedRectangleTests
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly ILogger<OrientedRectangleTests> _logger;
    private readonly FileSystemUrlPublisher<OrientedRectangleTests> _publisher;

    public OrientedRectangleTests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        _logger = new TestLogger<OrientedRectangleTests>(outputHelper);
        _publisher = new FileSystemUrlPublisher<OrientedRectangleTests>("/tmp/oriented-rectangle-debug", _logger);
    }

    [Fact]
    public async Task ComputeOrientedRectangle_WithSimpleSquarePoints_ReturnsValidRectangle()
    {
        // Arrange: Create a simple square rotated 45 degrees
        List<(int X, int Y)> points =
        [
            (50, 0), // Top
            (100, 50), // Right
            (50, 100), // Bottom
            (0, 50) // Left
        ];

        // Act: Get convex hull and compute oriented rectangle
        var orientedRect = BoundingRectangles.ComputeOrientedRectangle(points);

        // Create debug visualization
        using var debugImage = OrientedRectangleTestUtils.CreateDebugVisualization(points, orientedRect);
        await _publisher.PublishAsync(debugImage, "Debug oriented rectangle computation - black dots show convex hull, red polygon shows computed rectangle");

        _outputHelper.WriteLine($"Points: [{string.Join(", ", points)}]");
        _outputHelper.WriteLine($"Oriented rectangle: [{string.Join(", ", orientedRect)}]");

        // Assert: Verify rectangle properties
        Assert.Equal(4, orientedRect.Count);

        VerifyRectangleHasParallelSides(orientedRect);
        VerifyAllPointsContained(points, orientedRect);
        VerifyAtLeastTwoPointsOnBoundary(points, orientedRect);
    }

    [Fact]
    public async Task ComputeOrientedRectangle_WithRandomPointCloud_ReturnsValidRectangle()
    {
        // Arrange: Generate random points inside a circle
        var random = new Random(0); // Fixed seed for reproducible test
        var numIterations = 100;

        for (int n = 0; n < numIterations; n++)
        {
            var points = new List<(int X, int Y)>();

            for (int i = 0; i < 50; i++)
            {
                double angle = random.NextDouble() * 2 * Math.PI;
                double radius = random.NextDouble() * 80; // Radius 0-80
                int x = 100 + (int)(radius * Math.Cos(angle)); // Centered at (100, 75)
                int y = 75 + (int)(radius * Math.Sin(angle));
                points.Add((x, y));
            }

            // Act: Get convex hull and compute oriented rectangle
            var convexHull = ConvexHull.GrahamScan(points.ToArray());
            var orientedRect = BoundingRectangles.ComputeOrientedRectangle(convexHull);

            // Create debug visualization
            using var debugImage = OrientedRectangleTestUtils.CreateDebugVisualization(convexHull, orientedRect);
            await _publisher.PublishAsync(debugImage,
                "Random point cloud - convex hull points as black dots, oriented rectangle as red polygon");

            _outputHelper.WriteLine($"Generated {points.Count} random points");
            _outputHelper.WriteLine($"Convex hull has {convexHull.Count} points: [{string.Join(", ", convexHull)}]");
            _outputHelper.WriteLine($"Oriented rectangle: [{string.Join(", ", orientedRect)}]");

            // Assert: Verify rectangle properties
            Assert.Equal(4, orientedRect.Count);
            Assert.True(convexHull.Count >= 3, "Convex hull should have at least 3 points");
            Assert.True(convexHull.Count <= points.Count, "Convex hull cannot have more points than input");

            VerifyRectangleHasParallelSides(orientedRect);
            VerifyAllPointsContained(convexHull, orientedRect); // Use hull points, not all original points
            VerifyAtLeastTwoPointsOnBoundary(convexHull, orientedRect);
        }
    }

    private void VerifyRectangleHasParallelSides(List<(int X, int Y)> rectangle)
    {
        Assert.Equal(4, rectangle.Count);

        // Calculate the four edge vectors
        var edges = new List<(double X, double Y)>();
        for (int i = 0; i < 4; i++)
        {
            var current = rectangle[i];
            var next = rectangle[(i + 1) % 4];
            edges.Add((next.X - current.X, next.Y - current.Y));
        }

        // Calculate tolerances based on rectangle size (proportional to edge lengths)
        var edgeLengths = edges.Select(e => Math.Sqrt(e.X * e.X + e.Y * e.Y)).ToList();
        var avgEdgeLength = edgeLengths.Average();
        var parallelTolerance = Math.Max(2.0, avgEdgeLength * 0.02); // 2% of edge length or minimum 2 pixels
        var perpendicularTolerance = Math.Max(50.0, avgEdgeLength * avgEdgeLength * 0.015); // For dot product

        // Edge 0 should be parallel to edge 2
        Assert.True(AreVectorsParallel(edges[0], edges[2], parallelTolerance),
            $"Opposite edges should be parallel within tolerance {parallelTolerance:F1}: Edge0={edges[0]}, Edge2={edges[2]}");

        // Edge 1 should be parallel to edge 3
        Assert.True(AreVectorsParallel(edges[1], edges[3], parallelTolerance),
            $"Opposite edges should be parallel within tolerance {parallelTolerance:F1}: Edge1={edges[1]}, Edge3={edges[3]}");

        // Adjacent edges should be perpendicular (dot product should be close to 0)
        double dotProduct = edges[0].X * edges[1].X + edges[0].Y * edges[1].Y;
        Assert.True(Math.Abs(dotProduct) < perpendicularTolerance,
            $"Adjacent edges should be perpendicular within tolerance {perpendicularTolerance:F1}, dot product was {dotProduct}");
    }

    private bool AreVectorsParallel((double X, double Y) v1, (double X, double Y) v2, double tolerance)
    {
        // Two vectors are parallel if their cross product is zero
        double crossProduct = v1.X * v2.Y - v1.Y * v2.X;
        return Math.Abs(crossProduct) < tolerance;
    }

    private void VerifyAllPointsContained(List<(int X, int Y)> points, List<(int X, int Y)> rectangle)
    {
        foreach (var point in points)
        {
            Assert.True(IsPointInRectangle(point, rectangle),
                $"Point {point} should be contained within the oriented rectangle");
        }
    }

    private void VerifyAtLeastTwoPointsOnBoundary(List<(int X, int Y)> points, List<(int X, int Y)> rectangle)
    {
        int pointsOnBoundary = 0;
        var tolerance = 2.0;

        foreach (var point in points)
        {
            if (IsPointOnRectangleBoundary(point, rectangle, tolerance))
            {
                pointsOnBoundary++;
            }
        }

        Assert.True(pointsOnBoundary >= 2,
            $"At least 2 original points should lie within {tolerance} pixels of the rectangle boundary. Found {pointsOnBoundary} points on boundary.");
    }

    private bool IsPointInRectangle((int X, int Y) point, List<(int X, int Y)> rectangle)
    {
        // Use distance-based tolerance since rounding can make the integer rectangle
        // slightly smaller than the theoretical floating-point rectangle
        var tolerance = 3.0; // Allow 3 pixels of tolerance

        // Check if point is close to boundary first
        if (IsPointOnRectangleBoundary(point, rectangle, tolerance))
        {
            return true;
        }

        // Use point-in-polygon for interior points
        return IsPointInPolygon(point, rectangle);
    }

    private bool IsPointOnRectangleBoundary((int X, int Y) point, List<(int X, int Y)> rectangle, double tolerance)
    {
        // Check if point lies on any of the four edges
        for (int i = 0; i < 4; i++)
        {
            var edge1 = rectangle[i];
            var edge2 = rectangle[(i + 1) % 4];

            if (IsPointOnLineSegment(point, edge1, edge2, tolerance))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsPointOnLineSegment((int X, int Y) point, (int X, int Y) start, (int X, int Y) end, double tolerance)
    {
        // Calculate distance from point to line
        double A = end.Y - start.Y;
        double B = start.X - end.X;
        double C = end.X * start.Y - start.X * end.Y;

        double distance = Math.Abs(A * point.X + B * point.Y + C) / Math.Sqrt(A * A + B * B);

        if (distance > tolerance)
            return false;

        // Check if point is within the segment bounds
        double minX = Math.Min(start.X, end.X);
        double maxX = Math.Max(start.X, end.X);
        double minY = Math.Min(start.Y, end.Y);
        double maxY = Math.Max(start.Y, end.Y);

        return point.X >= minX - tolerance && point.X <= maxX + tolerance &&
               point.Y >= minY - tolerance && point.Y <= maxY + tolerance;
    }

    private bool IsPointInPolygon((int X, int Y) point, List<(int X, int Y)> polygon)
    {
        int intersectionCount = 0;
        int n = polygon.Count;

        for (int i = 0; i < n; i++)
        {
            var p1 = polygon[i];
            var p2 = polygon[(i + 1) % n];

            if (((p1.Y > point.Y) != (p2.Y > point.Y)) &&
                (point.X < (p2.X - p1.X) * (point.Y - p1.Y) / (p2.Y - p1.Y) + p1.X))
            {
                intersectionCount++;
            }
        }

        return (intersectionCount % 2) == 1;
    }
}
