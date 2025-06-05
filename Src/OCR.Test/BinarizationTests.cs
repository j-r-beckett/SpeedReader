#pragma warning disable
// Rider intellisense incorrectly flags tensor[i,j] indexing as syntax error in preview .NET
// This compiles correctly with both dotnet CLI and Rider solution build
using System.Numerics.Tensors;
using CommunityToolkit.HighPerformance;
using OCR.Algorithms;
using Xunit;

namespace OCR.Test;

public class BinarizationTests
{

    [Fact]
    public void BinarizeProbabilityMap_AppliesThresholdCorrectly()
    {
        // Arrange: Create probability map with known values
        var probabilityData = new float[]
        {
            0.1f, 0.2f, 0.3f,
            0.19f, 0.21f, 0.5f,
            0.0f, 0.8f, 1.0f
        };
        var tensor = Tensor.Create<float>(probabilityData, [3, 3]);

        // Act
        Binarization.BinarizeInPlace(tensor, 0.2f);

        // Assert: Values > 0.2 should be 1.0f, <= 0.2 should be 0.0f
        Assert.Equal(0.0f, tensor[0, 0]); // 0.1 <= 0.2
        Assert.Equal(0.0f, tensor[0, 1]); // 0.2 <= 0.2
        Assert.Equal(1.0f, tensor[0, 2]); // 0.3 > 0.2

        Assert.Equal(0.0f, tensor[1, 0]); // 0.19 <= 0.2
        Assert.Equal(1.0f, tensor[1, 1]); // 0.21 > 0.2
        Assert.Equal(1.0f, tensor[1, 2]); // 0.5 > 0.2

        Assert.Equal(0.0f, tensor[2, 0]); // 0.0 <= 0.2
        Assert.Equal(1.0f, tensor[2, 1]); // 0.8 > 0.2
        Assert.Equal(1.0f, tensor[2, 2]); // 1.0 > 0.2
    }

    [Fact]
    public void BinarizeProbabilityMap_HandlesEdgeCases()
    {
        // Arrange: Create probability map with edge cases
        var probabilityData = new float[]
        {
            0.2f, 0.200001f,
            0.199999f, 0.0f
        };
        var tensor = Tensor.Create<float>(probabilityData, [2, 2]);

        // Act
        Binarization.BinarizeInPlace(tensor, 0.2f);

        // Assert: Test exact threshold boundary and negative values
        Assert.Equal(0.0f, tensor[0, 0]); // Exactly 0.2 should be 0.0f
        Assert.Equal(1.0f, tensor[0, 1]); // Just above 0.2 should be 1.0f
        Assert.Equal(0.0f, tensor[1, 0]); // Just below 0.2 should be 0.0f
        Assert.Equal(0.0f, tensor[1, 1]); // Negative values should be 0.0f
    }

    [Fact]
    public void BinarizeProbabilityMap_PreservesMapDimensions()
    {
        // Arrange: Create maps of different sizes
        var smallTensor = Tensor.Create<float>(new float[2 * 3], [2, 3]);
        var largeTensor = Tensor.Create<float>(new float[100 * 200], [100, 200]);

        // Act
        Binarization.BinarizeInPlace(smallTensor, 0.2f);
        Binarization.BinarizeInPlace(largeTensor, 0.2f);

        // Assert: Dimensions should be preserved
        Assert.Equal(2, smallTensor.Lengths[0]);
        Assert.Equal(3, smallTensor.Lengths[1]);

        Assert.Equal(100, largeTensor.Lengths[0]);
        Assert.Equal(200, largeTensor.Lengths[1]);
    }

    [Fact]
    public void BinarizeProbabilityMap_HandlesSinglePixel()
    {
        // Arrange: Single pixel probability maps
        var highTensor = Tensor.Create<float>([0.9f], [1, 1]);
        var lowTensor = Tensor.Create<float>([0.1f], [1, 1]);

        // Act
        Binarization.BinarizeInPlace(highTensor, 0.2f);
        Binarization.BinarizeInPlace(lowTensor, 0.2f);

        // Assert
        Assert.Equal(1.0f, highTensor[0, 0]);
        Assert.Equal(0.0f, lowTensor[0, 0]);
    }
}
