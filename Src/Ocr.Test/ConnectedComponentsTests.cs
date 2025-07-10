using CommunityToolkit.HighPerformance;
using Ocr.Algorithms;

namespace Ocr.Test;

public class ConnectedComponentsTests
{
    [Fact]
    public void ConnectedComponents_SingleComponent_ReturnsSingleComponent()
    {
        var data = new float[,]
        {
            { 0f, 0f, 0f, 0f },
            { 0f, 1f, 1f, 0f },
            { 0f, 1f, 1f, 0f },
            { 0f, 0f, 0f, 0f }
        };
        var probabilityMap = CreateSpan2D(data);

        var components = ConnectedComponents.FindComponents(probabilityMap);

        Assert.Single(components);
        Assert.Equal(4, components[0].Length);

        var points = components[0].ToHashSet();
        Assert.Contains((1, 1), points);
        Assert.Contains((2, 1), points);
        Assert.Contains((1, 2), points);
        Assert.Contains((2, 2), points);
    }

    [Fact]
    public void ConnectedComponents_TwoSeparateComponents_ReturnsTwoComponents()
    {
        // Arrange
        var data = new float[,]
        {
            { 1f, 1f, 0f, 1f, 1f },
            { 1f, 1f, 0f, 1f, 1f },
            { 0f, 0f, 0f, 0f, 0f },
            { 0f, 0f, 0f, 0f, 0f }
        };
        var probabilityMap = CreateSpan2D(data);

        // Act
        var components = ConnectedComponents.FindComponents(probabilityMap);

        // Assert
        Assert.Equal(2, components.Count);

        // Sort components by size for consistent testing
        var sortedComponents = components.OrderBy(c => c.Length).ToArray();

        Assert.Equal(4, sortedComponents[0].Length); // Left component
        Assert.Equal(4, sortedComponents[1].Length); // Right component

        var leftPoints = sortedComponents[0].ToHashSet();
        var rightPoints = sortedComponents[1].ToHashSet();

        // Verify no overlap between components
        Assert.Empty(leftPoints.Intersect(rightPoints));

        // Verify all expected points are present
        var allPoints = leftPoints.Union(rightPoints);
        Assert.Equal(8, allPoints.Count());
    }

    [Fact]
    public void ConnectedComponents_EmptyData_ReturnsNoComponents()
    {
        // Arrange
        var data = new float[,]
        {
            { 0f, 0f, 0f },
            { 0f, 0f, 0f },
            { 0f, 0f, 0f }
        };
        var probabilityMap = CreateSpan2D(data);

        // Act
        var components = ConnectedComponents.FindComponents(probabilityMap);

        // Assert
        Assert.Empty(components);
    }

    [Fact]
    public void ConnectedComponents_DiagonalConnection_ConnectsDiagonalPixels()
    {
        // Arrange
        var data = new float[,]
        {
            { 1f, 0f, 0f },
            { 0f, 1f, 0f },
            { 0f, 0f, 1f }
        };
        var probabilityMap = CreateSpan2D(data);

        // Act
        var components = ConnectedComponents.FindComponents(probabilityMap);

        // Assert
        Assert.Single(components);
        Assert.Equal(3, components[0].Length);

        var points = components[0].ToHashSet();
        Assert.Contains((0, 0), points);
        Assert.Contains((1, 1), points);
        Assert.Contains((2, 2), points);
    }

    [Fact]
    public void ConnectedComponents_LShapeComponent_HandlesComplexShape()
    {
        // Arrange
        var data = new float[,]
        {
            { 1f, 1f, 1f },
            { 1f, 0f, 0f },
            { 1f, 0f, 0f }
        };
        var probabilityMap = CreateSpan2D(data);

        // Act
        var components = ConnectedComponents.FindComponents(probabilityMap);

        // Assert
        Assert.Single(components);
        Assert.Equal(5, components[0].Length);

        var points = components[0].ToHashSet();
        Assert.Contains((0, 0), points);
        Assert.Contains((1, 0), points);
        Assert.Contains((2, 0), points);
        Assert.Contains((0, 1), points);
        Assert.Contains((0, 2), points);
    }

    [Fact]
    public void ConnectedComponents_SinglePixel_ReturnsSinglePointComponent()
    {
        // Arrange
        var data = new float[,]
        {
            { 0f, 0f, 0f },
            { 0f, 1f, 0f },
            { 0f, 0f, 0f }
        };
        var probabilityMap = CreateSpan2D(data);

        // Act
        var components = ConnectedComponents.FindComponents(probabilityMap);

        // Assert
        Assert.Single(components);
        Assert.Single(components[0]);
        Assert.Equal((1, 1), components[0][0]);
    }

    [Fact]
    public void ConnectedComponents_EdgePixels_HandlesEdgeCases()
    {
        // Arrange: Test pixels on all edges
        var data = new float[,]
        {
            { 1f, 0f, 1f },
            { 0f, 0f, 0f },
            { 1f, 0f, 1f }
        };
        var probabilityMap = CreateSpan2D(data);

        // Act
        var components = ConnectedComponents.FindComponents(probabilityMap);

        // Assert
        Assert.Equal(4, components.Count);

        foreach (var component in components)
        {
            Assert.Single(component);
        }

        var allPoints = components.SelectMany(c => c).ToHashSet();
        Assert.Contains((0, 0), allPoints);
        Assert.Contains((2, 0), allPoints);
        Assert.Contains((0, 2), allPoints);
        Assert.Contains((2, 2), allPoints);
    }

    [Fact]
    public void ConnectedComponents_MinimumSize_HandlesSinglePixelArray()
    {
        // Arrange
        var data = new float[,] { { 1f } };
        var probabilityMap = CreateSpan2D(data);

        // Act
        var components = ConnectedComponents.FindComponents(probabilityMap);

        // Assert
        Assert.Single(components);
        Assert.Single(components[0]);
        Assert.Equal((0, 0), components[0][0]);
    }

    [Fact]
    public void ConnectedComponents_ComponentWithHole_TreatsAsOneComponent()
    {
        // Arrange
        var data = new float[,]
        {
            { 1f, 1f, 1f, 1f, 1f },
            { 1f, 0f, 0f, 0f, 1f },
            { 1f, 0f, 0f, 0f, 1f },
            { 1f, 0f, 0f, 0f, 1f },
            { 1f, 1f, 1f, 1f, 1f }
        };
        var probabilityMap = CreateSpan2D(data);

        // Act
        var components = ConnectedComponents.FindComponents(probabilityMap);

        // Assert
        Assert.Single(components);
        Assert.Equal(16, components[0].Length); // 5x5 - 3x3 hole = 16 pixels

        var points = components[0].ToHashSet();

        // Verify outer border is included
        Assert.Contains((0, 0), points);
        Assert.Contains((4, 0), points);
        Assert.Contains((0, 4), points);
        Assert.Contains((4, 4), points);

        // Verify hole pixels are not included
        Assert.DoesNotContain((2, 2), points); // Center of hole
        Assert.DoesNotContain((1, 1), points); // Corner of hole
        Assert.DoesNotContain((3, 3), points); // Opposite corner of hole
    }

    [Fact]
    public void ConnectedComponents_VariousProbabilityValues_DetectsPositiveValues()
    {
        // Arrange
        var data = new float[,]
        {
            { 0.2f, 0f, 1.5f },
            { 0f, 0.8f, 0f },
            { 2.3f, 0f, 0.1f }
        };
        var probabilityMap = CreateSpan2D(data);

        // Act
        var components = ConnectedComponents.FindComponents(probabilityMap);

        // Assert
        Assert.Single(components); // All positive values are connected via 8-connectivity
        Assert.Equal(5, components[0].Length); // Should contain all 5 positive pixels

        var points = components[0].ToHashSet();
        Assert.Contains((0, 0), points); // 0.2f
        Assert.Contains((2, 0), points); // 1.5f
        Assert.Contains((1, 1), points); // 0.8f
        Assert.Contains((0, 2), points); // 2.3f
        Assert.Contains((2, 2), points); // 0.1f
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
}
