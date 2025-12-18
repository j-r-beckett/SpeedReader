// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Ocr.Geometry;

namespace Ocr.Test.Geometry;

public class PolygonTests
{
    #region Dilate
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Dilate_WithInsufficientPoints_ReturnsNull(int pointCount)
    {
        var points = new List<PointF>();
        for (int i = 0; i < pointCount; i++)
        {
            points.Add((i * 10.0, i * 10.0));
        }
        var polygon = new Polygon(points);

        var result = polygon.Dilate(0.5);

        Assert.Null(result);
    }

    [Fact]
    public void Dilate_SquareWithPositiveRatio_IncreasesArea()
    {
        var polygon = new Polygon([
            (0.0, 0.0),
            (10.0, 0.0),
            (10.0, 10.0),
            (0.0, 10.0)
        ]);

        var originalArea = CalculateArea(polygon.Points);
        var dilated = polygon.Dilate(1.0);

        Assert.NotNull(dilated);
        var dilatedArea = CalculateArea(dilated.Points);
        Assert.True(dilatedArea > originalArea,
            $"Dilated area {dilatedArea:F2} should be greater than original area {originalArea:F2}");
    }

    [Fact]
    public void Dilate_SquareWithNegativeRatio_DecreasesArea()
    {
        var polygon = new Polygon([
            (0.0, 0.0),
            (100.0, 0.0),
            (100.0, 100.0),
            (0.0, 100.0)
        ]);

        var originalArea = CalculateArea(polygon.Points);
        var dilated = polygon.Dilate(-0.3);

        Assert.NotNull(dilated);
        var dilatedArea = CalculateArea(dilated.Points);
        Assert.True(dilatedArea < originalArea,
            $"Dilated area {dilatedArea:F2} should be less than original area {originalArea:F2}");
    }

    [Fact]
    public void Dilate_SquareWithZeroRatio_PreservesApproximateArea()
    {
        var polygon = new Polygon([
            (0.0, 0.0),
            (50.0, 0.0),
            (50.0, 50.0),
            (0.0, 50.0)
        ]);

        var originalArea = CalculateArea(polygon.Points);
        var dilated = polygon.Dilate(0.0);

        Assert.NotNull(dilated);
        var dilatedArea = CalculateArea(dilated.Points);
        var tolerance = originalArea * 0.1;
        Assert.True(Math.Abs(dilatedArea - originalArea) < tolerance,
            $"Dilated area {dilatedArea:F2} should be approximately equal to original area {originalArea:F2}");
    }

    [Fact]
    public void Dilate_TriangleWithPositiveRatio_IncreasesArea()
    {
        var polygon = new Polygon([
            (0.0, 0.0),
            (10.0, 0.0),
            (5.0, 8.0)
        ]);

        var originalArea = CalculateArea(polygon.Points);
        var dilated = polygon.Dilate(0.5);

        Assert.NotNull(dilated);
        var dilatedArea = CalculateArea(dilated.Points);
        Assert.True(dilatedArea > originalArea,
            $"Dilated area {dilatedArea:F2} should be greater than original area {originalArea:F2}");
    }

    [Fact]
    public void Dilate_SquareWithLargeNegativeRatio_ReturnsNull()
    {
        var polygon = new Polygon([
            (0.0, 0.0),
            (10.0, 0.0),
            (10.0, 10.0),
            (0.0, 10.0)
        ]);

        var result = polygon.Dilate(-5.0);

        Assert.Null(result);
    }

    [Fact]
    public void Dilate_ComplexPolygonWithPositiveRatio_IncreasesArea()
    {
        var polygon = new Polygon([
            (10.0, 10.0),
            (40.0, 10.0),
            (40.0, 30.0),
            (30.0, 30.0),
            (30.0, 20.0),
            (20.0, 20.0),
            (20.0, 30.0),
            (10.0, 30.0)
        ]);

        var originalArea = CalculateArea(polygon.Points);
        var dilated = polygon.Dilate(0.5);

        Assert.NotNull(dilated);
        var dilatedArea = CalculateArea(dilated.Points);
        Assert.True(dilatedArea > originalArea,
            $"Dilated area {dilatedArea:F2} should be greater than original area {originalArea:F2}");
    }
    #endregion Dilate

    #region Clamp
    [Fact]
    public void Clamp_PolygonCompletelyInside_NoChange()
    {
        var polygon = new Polygon([
            (10.0, 10.0),
            (50.0, 10.0),
            (50.0, 40.0),
            (10.0, 40.0)
        ]);

        var clamped = polygon.Clamp(height: 100, width: 100);

        Assert.Equal(4, clamped.Points.Count);
        AssertPointEquals((10.0, 10.0), clamped.Points[0]);
        AssertPointEquals((50.0, 10.0), clamped.Points[1]);
        AssertPointEquals((50.0, 40.0), clamped.Points[2]);
        AssertPointEquals((10.0, 40.0), clamped.Points[3]);
    }

    [Fact]
    public void Clamp_PolygonCompletelyOutside_ClampsToEdges()
    {
        var polygon = new Polygon([
            (-10.0, -10.0),
            (150.0, -10.0),
            (150.0, 150.0),
            (-10.0, 150.0)
        ]);

        var clamped = polygon.Clamp(height: 100, width: 100);

        Assert.Equal(4, clamped.Points.Count);
        AssertPointEquals((0.0, 0.0), clamped.Points[0]);
        AssertPointEquals((100.0, 0.0), clamped.Points[1]);
        AssertPointEquals((100.0, 100.0), clamped.Points[2]);
        AssertPointEquals((0.0, 100.0), clamped.Points[3]);
    }

    [Fact]
    public void Clamp_PolygonPartiallyOutside_ClampsOnlyOutliers()
    {
        var polygon = new Polygon([
            (-5.0, 20.0),
            (50.0, 20.0),
            (50.0, 110.0),
            (-5.0, 110.0)
        ]);

        var clamped = polygon.Clamp(height: 100, width: 100);

        Assert.Equal(4, clamped.Points.Count);
        AssertPointEquals((0.0, 20.0), clamped.Points[0]);
        AssertPointEquals((50.0, 20.0), clamped.Points[1]);
        AssertPointEquals((50.0, 100.0), clamped.Points[2]);
        AssertPointEquals((0.0, 100.0), clamped.Points[3]);
    }

    [Fact]
    public void Clamp_WithNegativeCoordinates_ClampsToZero()
    {
        var polygon = new Polygon([
            (-20.0, -30.0),
            (50.0, -10.0),
            (30.0, 50.0)
        ]);

        var clamped = polygon.Clamp(height: 100, width: 100);

        Assert.Equal(3, clamped.Points.Count);
        AssertPointEquals((0.0, 0.0), clamped.Points[0]);
        AssertPointEquals((50.0, 0.0), clamped.Points[1]);
        AssertPointEquals((30.0, 50.0), clamped.Points[2]);
    }

    [Fact]
    public void Clamp_WithCoordinatesBeyondBounds_ClampsToMaxBounds()
    {
        var polygon = new Polygon([
            (50.0, 50.0),
            (150.0, 50.0),
            (150.0, 200.0),
            (50.0, 200.0)
        ]);

        var clamped = polygon.Clamp(height: 100, width: 100);

        Assert.Equal(4, clamped.Points.Count);
        AssertPointEquals((50.0, 50.0), clamped.Points[0]);
        AssertPointEquals((100.0, 50.0), clamped.Points[1]);
        AssertPointEquals((100.0, 100.0), clamped.Points[2]);
        AssertPointEquals((50.0, 100.0), clamped.Points[3]);
    }
    #endregion Clamp

    #region Scale
    [Theory]
    [InlineData(1.0, 10.0, 20.0, 50.0, 40.0)]
    [InlineData(2.0, 20.0, 40.0, 100.0, 80.0)]
    [InlineData(0.5, 5.0, 10.0, 25.0, 20.0)]
    [InlineData(0.0, 0.0, 0.0, 0.0, 0.0)]
    [InlineData(-1.0, -10.0, -20.0, -50.0, -40.0)]
    public void Scale_WithVariousScaleFactors_ScalesAllCoordinates(
        double scale,
        double expectedX1, double expectedY1,
        double expectedX2, double expectedY2)
    {
        var polygon = new Polygon([
            (10.0, 20.0),
            (50.0, 40.0)
        ]);

        var scaled = polygon.Scale(scale);

        Assert.Equal(2, scaled.Points.Count);
        AssertPointEquals((expectedX1, expectedY1), scaled.Points[0]);
        AssertPointEquals((expectedX2, expectedY2), scaled.Points[1]);
    }
    #endregion Scale

    #region Simplify
    [Fact]
    public void Simplify_StraightLine_ReducesToMinimumPoints()
    {
        var polygon = new Polygon([
            (0.0, 0.0),
            (5.0, 5.0),
            (10.0, 10.0),
            (15.0, 15.0),
            (20.0, 20.0)
        ]);

        var simplified = polygon.Simplify(epsilon: 1.0);

        // Simplification keeps minimum 3 points for a valid polygon
        Assert.True(simplified.Points.Count <= 3,
            $"Collinear points should be reduced to minimum (3 or fewer), got {simplified.Points.Count}");
        Assert.True(simplified.Points.Count < polygon.Points.Count,
            "Point count should decrease");
    }

    [Fact]
    public void Simplify_RectangleWithMidpoints_RemovesMidpoints()
    {
        var polygon = new Polygon([
            (0.0, 0.0),
            (5.0, 0.0),     // midpoint on top edge
            (10.0, 0.0),
            (10.0, 5.0),    // midpoint on right edge
            (10.0, 10.0),
            (5.0, 10.0),    // midpoint on bottom edge
            (0.0, 10.0),
            (0.0, 5.0)      // midpoint on left edge
        ]);

        var simplified = polygon.Simplify(epsilon: 1.0);

        // Should remove at least some of the midpoints (collinear points)
        Assert.True(simplified.Points.Count < polygon.Points.Count,
            $"Point count should decrease, got {simplified.Points.Count} from {polygon.Points.Count}");
        Assert.True(simplified.Points.Count <= 5,
            $"Should remove most collinear midpoints, got {simplified.Points.Count}");
    }

    [Fact]
    public void Simplify_Triangle_RemainsUnchanged()
    {
        var polygon = new Polygon([
            (0.0, 0.0),
            (10.0, 0.0),
            (5.0, 8.0)
        ]);

        var simplified = polygon.Simplify(epsilon: 1.0);

        Assert.Equal(3, simplified.Points.Count);
        AssertPointEquals((0.0, 0.0), simplified.Points[0]);
        AssertPointEquals((10.0, 0.0), simplified.Points[1]);
        AssertPointEquals((5.0, 8.0), simplified.Points[2]);
    }

    [Fact]
    public void Simplify_RandomPolygons_DecreasesOrMaintainsPointCount()
    {
        var random = new Random(0);
        var numIterations = 10000;

        for (int n = 0; n < numIterations; n++)
        {
            var pointCount = 10 + random.Next(40);
            var points = new List<PointF>();

            for (int i = 0; i < pointCount; i++)
            {
                double angle = random.NextDouble() * 2 * Math.PI;
                double radius = 20 + random.NextDouble() * 60;
                double x = 100 + radius * Math.Cos(angle);
                double y = 100 + radius * Math.Sin(angle);
                points.Add((x, y));
            }

            var polygon = new Polygon(points);
            var epsilon = 1 + random.NextDouble() * 5;
            var simplified = polygon.Simplify(epsilon);

            Assert.True(simplified.Points.Count <= polygon.Points.Count,
                $"Iteration {n}: Simplified point count {simplified.Points.Count} should be ≤ original {polygon.Points.Count}");
            Assert.True(simplified.Points.Count >= 2,
                $"Iteration {n}: Simplified polygon should have at least 2 points");
        }
    }
    #endregion Simplify

    #region Utils
    private void AssertPointEquals((double X, double Y) expected, PointF actual, double tolerance = 1e-10)
    {
        Assert.True(Math.Abs(expected.X - actual.X) < tolerance,
            $"X mismatch: expected {expected.X:F10}, got {actual.X:F10}");
        Assert.True(Math.Abs(expected.Y - actual.Y) < tolerance,
            $"Y mismatch: expected {expected.Y:F10}, got {actual.Y:F10}");
    }

    private double CalculateArea(IReadOnlyList<PointF> points)
    {
        if (points.Count < 3)
            return 0;

        double area = 0;
        for (int i = 0; i < points.Count; i++)
        {
            var current = points[i];
            var next = points[(i + 1) % points.Count];
            area += current.X * next.Y - next.X * current.Y;
        }
        return Math.Abs(area) / 2;
    }
    #endregion Utils
}
