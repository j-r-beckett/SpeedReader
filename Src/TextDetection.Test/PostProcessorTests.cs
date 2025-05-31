using Xunit;

namespace TextDetection.Test;

public class PostProcessorTests
{
    [Fact]
    public void BinarizeProbabilityMap_AppliesThresholdCorrectly()
    {
        // Arrange: Create probability map with known values
        var probabilityMap = new float[3, 3]
        {
            { 0.1f, 0.2f, 0.3f },
            { 0.19f, 0.21f, 0.5f },
            { 0.0f, 0.8f, 1.0f }
        };

        // Act
        var binaryMap = PostProcessor.BinarizeProbabilityMap(probabilityMap);

        // Assert: Values > 0.2 should be true, <= 0.2 should be false
        Assert.False(binaryMap[0, 0]); // 0.1 <= 0.2
        Assert.False(binaryMap[0, 1]); // 0.2 <= 0.2
        Assert.True(binaryMap[0, 2]);  // 0.3 > 0.2
        
        Assert.False(binaryMap[1, 0]); // 0.19 <= 0.2
        Assert.True(binaryMap[1, 1]);  // 0.21 > 0.2
        Assert.True(binaryMap[1, 2]);  // 0.5 > 0.2
        
        Assert.False(binaryMap[2, 0]); // 0.0 <= 0.2
        Assert.True(binaryMap[2, 1]);  // 0.8 > 0.2
        Assert.True(binaryMap[2, 2]);  // 1.0 > 0.2
    }

    [Fact]
    public void BinarizeProbabilityMap_HandlesEdgeCases()
    {
        // Arrange: Create probability map with edge cases
        var probabilityMap = new float[2, 2]
        {
            { 0.2f, 0.200001f },
            { 0.199999f, -0.1f }
        };

        // Act
        var binaryMap = PostProcessor.BinarizeProbabilityMap(probabilityMap);

        // Assert: Test exact threshold boundary and negative values
        Assert.False(binaryMap[0, 0]); // Exactly 0.2 should be false
        Assert.True(binaryMap[0, 1]);  // Just above 0.2 should be true
        Assert.False(binaryMap[1, 0]); // Just below 0.2 should be false
        Assert.False(binaryMap[1, 1]); // Negative values should be false
    }

    [Fact]
    public void BinarizeProbabilityMap_PreservesMapDimensions()
    {
        // Arrange: Create maps of different sizes
        var smallMap = new float[2, 3];
        var largeMap = new float[100, 200];

        // Act
        var smallBinary = PostProcessor.BinarizeProbabilityMap(smallMap);
        var largeBinary = PostProcessor.BinarizeProbabilityMap(largeMap);

        // Assert: Dimensions should be preserved
        Assert.Equal(2, smallBinary.GetLength(0));
        Assert.Equal(3, smallBinary.GetLength(1));
        
        Assert.Equal(100, largeBinary.GetLength(0));
        Assert.Equal(200, largeBinary.GetLength(1));
    }

    [Fact]
    public void BinarizeProbabilityMap_HandlesSinglePixel()
    {
        // Arrange: Single pixel probability maps
        var highProb = new float[1, 1] { { 0.9f } };
        var lowProb = new float[1, 1] { { 0.1f } };

        // Act
        var highBinary = PostProcessor.BinarizeProbabilityMap(highProb);
        var lowBinary = PostProcessor.BinarizeProbabilityMap(lowProb);

        // Assert
        Assert.True(highBinary[0, 0]);
        Assert.False(lowBinary[0, 0]);
    }
}