// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Ocr.Algorithms;

namespace Ocr.Test;

public class ReliefMapBinarizeTests
{
    [Fact]
    public void Binarize_AppliesThresholdCorrectly()
    {
        // Arrange: Create relief map with known values
        var data = new float[]
        {
            0.1f, 0.2f, 0.3f,
            0.19f, 0.21f, 0.5f,
            0.0f, 0.8f, 1.0f
        };
        var map = new ReliefMap(data, width: 3, height: 3);

        // Act
        map.Binarize(0.2f);

        // Assert: Values >= 0.2 should be 1.0f, < 0.2 should be 0.0f
        Assert.Equal(0.0f, map[0, 0]); // 0.1 < 0.2
        Assert.Equal(1.0f, map[1, 0]); // 0.2 >= 0.2
        Assert.Equal(1.0f, map[2, 0]); // 0.3 >= 0.2

        Assert.Equal(0.0f, map[0, 1]); // 0.19 < 0.2
        Assert.Equal(1.0f, map[1, 1]); // 0.21 >= 0.2
        Assert.Equal(1.0f, map[2, 1]); // 0.5 >= 0.2

        Assert.Equal(0.0f, map[0, 2]); // 0.0 < 0.2
        Assert.Equal(1.0f, map[1, 2]); // 0.8 >= 0.2
        Assert.Equal(1.0f, map[2, 2]); // 1.0 >= 0.2
    }

    [Fact]
    public void Binarize_HandlesEdgeCases()
    {
        // Arrange: Create relief map with edge cases
        var data = new float[]
        {
            0.2f, 0.200001f,
            0.199999f, 0.0f
        };
        var map = new ReliefMap(data, width: 2, height: 2);

        // Act
        map.Binarize(0.2f);

        // Assert: Test exact threshold boundary
        Assert.Equal(1.0f, map[0, 0]); // Exactly 0.2 should be 1.0f
        Assert.Equal(1.0f, map[1, 0]); // Just above 0.2 should be 1.0f
        Assert.Equal(0.0f, map[0, 1]); // Just below 0.2 should be 0.0f
        Assert.Equal(0.0f, map[1, 1]); // 0.0 should be 0.0f
    }

    [Fact]
    public void Binarize_PreservesMapDimensions()
    {
        // Arrange: Create maps of different sizes
        var smallData = new float[2 * 3];
        var largeData = new float[100 * 200];
        var smallMap = new ReliefMap(smallData, width: 3, height: 2);
        var largeMap = new ReliefMap(largeData, width: 200, height: 100);

        // Act
        smallMap.Binarize(0.2f);
        largeMap.Binarize(0.2f);

        // Assert: Dimensions should be preserved
        Assert.Equal(3, smallMap.Width);
        Assert.Equal(2, smallMap.Height);

        Assert.Equal(200, largeMap.Width);
        Assert.Equal(100, largeMap.Height);
    }

    [Fact]
    public void Binarize_HandlesSinglePixel()
    {
        // Arrange: Single pixel relief maps
        var highData = new float[] { 0.9f };
        var lowData = new float[] { 0.1f };
        var highMap = new ReliefMap(highData, width: 1, height: 1);
        var lowMap = new ReliefMap(lowData, width: 1, height: 1);

        // Act
        highMap.Binarize(0.2f);
        lowMap.Binarize(0.2f);

        // Assert
        Assert.Equal(1.0f, highMap[0, 0]);
        Assert.Equal(0.0f, lowMap[0, 0]);
    }

    [Fact]
    public void Binarize_AllZeros_RemainsZeros()
    {
        // Arrange: All zeros
        var data = new float[10 * 10];
        var map = new ReliefMap(data, width: 10, height: 10);

        // Act
        map.Binarize(0.5f);

        // Assert: All values should remain 0.0f
        for (int y = 0; y < 10; y++)
        {
            for (int x = 0; x < 10; x++)
            {
                Assert.Equal(0.0f, map[x, y]);
            }
        }
    }

    [Fact]
    public void Binarize_AllOnes_RemainsOnes()
    {
        // Arrange: All ones
        var data = Enumerable.Repeat(1.0f, 10 * 10).ToArray();
        var map = new ReliefMap(data, width: 10, height: 10);

        // Act
        map.Binarize(0.5f);

        // Assert: All values should remain 1.0f
        for (int y = 0; y < 10; y++)
        {
            for (int x = 0; x < 10; x++)
            {
                Assert.Equal(1.0f, map[x, y]);
            }
        }
    }

    [Fact]
    public void Binarize_DifferentThresholds_ProducesDifferentResults()
    {
        // Arrange: Same data, different thresholds
        var data1 = new float[] { 0.3f, 0.5f, 0.7f };
        var data2 = new float[] { 0.3f, 0.5f, 0.7f };
        var map1 = new ReliefMap(data1, width: 3, height: 1);
        var map2 = new ReliefMap(data2, width: 3, height: 1);

        // Act
        map1.Binarize(0.4f);
        map2.Binarize(0.6f);

        // Assert: Different thresholds produce different results
        Assert.Equal(0.0f, map1[0, 0]); // 0.3 < 0.4
        Assert.Equal(1.0f, map1[1, 0]); // 0.5 >= 0.4
        Assert.Equal(1.0f, map1[2, 0]); // 0.7 >= 0.4

        Assert.Equal(0.0f, map2[0, 0]); // 0.3 < 0.6
        Assert.Equal(0.0f, map2[1, 0]); // 0.5 < 0.6
        Assert.Equal(1.0f, map2[2, 0]); // 0.7 >= 0.6
    }

    [Fact]
    public void Binarize_LargeMap_WorksCorrectly()
    {
        // Arrange: Large map to test vectorization
        var data = new float[1000 * 1000];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (i % 10) / 10.0f; // Values from 0.0 to 0.9
        }
        var map = new ReliefMap(data, width: 1000, height: 1000);

        // Act
        map.Binarize(0.5f);

        // Assert: Check that threshold was applied consistently
        for (int i = 0; i < data.Length; i++)
        {
            float originalValue = (i % 10) / 10.0f;
            float expected = originalValue >= 0.5f ? 1.0f : 0.0f;
            Assert.Equal(expected, data[i]);
        }
    }
}
