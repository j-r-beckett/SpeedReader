using CommunityToolkit.HighPerformance;
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
        var span2D = new Span2D<float>(probabilityMap);

        // Act
        Binarization.BinarizeInPlace(span2D, 0.2f);

        // Assert: Values > 0.2 should be 1.0f, <= 0.2 should be 0.0f
        Assert.Equal(0.0f, probabilityMap[0, 0]); // 0.1 <= 0.2
        Assert.Equal(0.0f, probabilityMap[0, 1]); // 0.2 <= 0.2
        Assert.Equal(1.0f, probabilityMap[0, 2]); // 0.3 > 0.2
        
        Assert.Equal(0.0f, probabilityMap[1, 0]); // 0.19 <= 0.2
        Assert.Equal(1.0f, probabilityMap[1, 1]); // 0.21 > 0.2
        Assert.Equal(1.0f, probabilityMap[1, 2]); // 0.5 > 0.2
        
        Assert.Equal(0.0f, probabilityMap[2, 0]); // 0.0 <= 0.2
        Assert.Equal(1.0f, probabilityMap[2, 1]); // 0.8 > 0.2
        Assert.Equal(1.0f, probabilityMap[2, 2]); // 1.0 > 0.2
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
        var span2D = new Span2D<float>(probabilityMap);

        // Act
        Binarization.BinarizeInPlace(span2D, 0.2f);

        // Assert: Test exact threshold boundary and negative values
        Assert.Equal(0.0f, probabilityMap[0, 0]); // Exactly 0.2 should be 0.0f
        Assert.Equal(1.0f, probabilityMap[0, 1]); // Just above 0.2 should be 1.0f
        Assert.Equal(0.0f, probabilityMap[1, 0]); // Just below 0.2 should be 0.0f
        Assert.Equal(0.0f, probabilityMap[1, 1]); // Negative values should be 0.0f
    }

    [Fact]
    public void BinarizeProbabilityMap_PreservesMapDimensions()
    {
        // Arrange: Create maps of different sizes
        var smallMap = new float[2, 3];
        var largeMap = new float[100, 200];
        var smallSpan = new Span2D<float>(smallMap);
        var largeSpan = new Span2D<float>(largeMap);

        // Act
        Binarization.BinarizeInPlace(smallSpan, 0.2f);
        Binarization.BinarizeInPlace(largeSpan, 0.2f);

        // Assert: Dimensions should be preserved
        Assert.Equal(2, smallMap.GetLength(0));
        Assert.Equal(3, smallMap.GetLength(1));
        
        Assert.Equal(100, largeMap.GetLength(0));
        Assert.Equal(200, largeMap.GetLength(1));
    }

    [Fact]
    public void BinarizeProbabilityMap_HandlesSinglePixel()
    {
        // Arrange: Single pixel probability maps
        var highProb = new float[1, 1] { { 0.9f } };
        var lowProb = new float[1, 1] { { 0.1f } };
        var highSpan = new Span2D<float>(highProb);
        var lowSpan = new Span2D<float>(lowProb);

        // Act
        Binarization.BinarizeInPlace(highSpan, 0.2f);
        Binarization.BinarizeInPlace(lowSpan, 0.2f);

        // Assert
        Assert.Equal(1.0f, highProb[0, 0]);
        Assert.Equal(0.0f, lowProb[0, 0]);
    }
}