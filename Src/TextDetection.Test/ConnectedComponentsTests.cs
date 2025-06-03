using System.Numerics.Tensors;
using System.Buffers;
using Xunit;

namespace TextDetection.Test;

public class ConnectedComponentsTests
{
    /// <summary>
    /// Helper method to convert a 2D array to a [1, H, W] tensor for testing
    /// </summary>
    private static TensorSpan<float> CreateBatchTensor(float[,] data)
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
        
        ReadOnlySpan<nint> shape = [1, height, width];
        var tensor = Tensor.Create(flatData, shape);
        return tensor.AsTensorSpan();
    }
    [Fact]
    public void ConnectedComponents_SingleComponent_ReturnsSingleComponent()
    {
        // Arrange
        var data = new float[,]
        {
            { 0f, 0f, 0f, 0f },
            { 0f, 1f, 1f, 0f },
            { 0f, 1f, 1f, 0f },
            { 0f, 0f, 0f, 0f }
        };
        var batchTensor = CreateBatchTensor(data);

        // Act
        var components = ConnectedComponentAnalysis.FindComponents(batchTensor);

        // Assert
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
            { 1f, 0f, 0f, 1f },
            { 0f, 0f, 0f, 0f },
            { 0f, 0f, 0f, 0f },
            { 1f, 0f, 0f, 1f }
        };
        var batchTensor = CreateBatchTensor(data);

        // Act
        var components = ConnectedComponentAnalysis.FindComponents(batchTensor);

        // Assert
        Assert.Equal(4, components.Length);
        
        foreach (var component in components)
        {
            Assert.Single(component);
        }

        var allPoints = components.SelectMany(c => c).ToHashSet();
        Assert.Contains((0, 0), allPoints);
        Assert.Contains((3, 0), allPoints);
        Assert.Contains((0, 3), allPoints);
        Assert.Contains((3, 3), allPoints);
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
        var batchTensor = CreateBatchTensor(data);

        // Act
        var components = ConnectedComponentAnalysis.FindComponents(batchTensor);

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
        var batchTensor = CreateBatchTensor(data);

        // Act
        var components = ConnectedComponentAnalysis.FindComponents(batchTensor);

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
        var batchTensor = CreateBatchTensor(data);

        // Act
        var components = ConnectedComponentAnalysis.FindComponents(batchTensor);

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
        var batchTensor = CreateBatchTensor(data);

        // Act
        var components = ConnectedComponentAnalysis.FindComponents(batchTensor);

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
        var batchTensor = CreateBatchTensor(data);

        // Act
        var components = ConnectedComponentAnalysis.FindComponents(batchTensor);

        // Assert
        Assert.Equal(4, components.Length);
        
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
        var batchTensor = CreateBatchTensor(data);

        // Act
        var components = ConnectedComponentAnalysis.FindComponents(batchTensor);

        // Assert
        Assert.Single(components);
        Assert.Single(components[0]);
        Assert.Equal((0, 0), components[0][0]);
    }
}