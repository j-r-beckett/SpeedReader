using Ocr.Algorithms;
using NumericsTensor = System.Numerics.Tensors.Tensor;

namespace Ocr.Test;

public class TensorOpsTests
{
    [Fact]
    public void ExtractProbabilityMaps_SingleBatch_ExtractsCorrectly()
    {
        // Arrange: Create a 3D NHW Tensor<float> with known values
        var tensorData = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
        ReadOnlySpan<nint> shape = [1, 2, 2]; // NHW: batch=1, height=2, width=2
        var tensor = NumericsTensor.Create(tensorData, shape);

        // Act
        var result = TensorTestUtils.ExtractProbabilityMapsForTesting(tensor);

        // Assert
        Assert.Single(result);
        Assert.Equal(2, result[0].GetLength(0)); // height
        Assert.Equal(2, result[0].GetLength(1)); // width

        Assert.Equal(0.1f, result[0][0, 0]);
        Assert.Equal(0.2f, result[0][0, 1]);
        Assert.Equal(0.3f, result[0][1, 0]);
        Assert.Equal(0.4f, result[0][1, 1]);
    }

    [Fact]
    public void ExtractProbabilityMaps_MultipleBatch_ExtractsAllBatches()
    {
        // Arrange: Create a 3D NHW Tensor<float> with 2 batches
        var tensorData = new float[]
        {
            // Batch 0
            0.1f, 0.2f, 0.3f, 0.4f,
            // Batch 1  
            0.5f, 0.6f, 0.7f, 0.8f
        };
        ReadOnlySpan<nint> shape = [2, 2, 2]; // NHW: batch=2, height=2, width=2
        var tensor = NumericsTensor.Create(tensorData, shape);

        // Act
        var result = TensorTestUtils.ExtractProbabilityMapsForTesting(tensor);

        // Assert
        Assert.Equal(2, result.Length);

        // Batch 0
        Assert.Equal(0.1f, result[0][0, 0]);
        Assert.Equal(0.2f, result[0][0, 1]);
        Assert.Equal(0.3f, result[0][1, 0]);
        Assert.Equal(0.4f, result[0][1, 1]);

        // Batch 1
        Assert.Equal(0.5f, result[1][0, 0]);
        Assert.Equal(0.6f, result[1][0, 1]);
        Assert.Equal(0.7f, result[1][1, 0]);
        Assert.Equal(0.8f, result[1][1, 1]);
    }

    [Fact]
    public void ExtractProbabilityMaps_LargerTensor_MaintainsRowColumnOrder()
    {
        // Arrange: Create a 3D NHW Tensor<float> to test row/column ordering
        var tensorData = new float[]
        {
            // Row 0
            1.0f, 2.0f, 3.0f, 4.0f,
            // Row 1
            5.0f, 6.0f, 7.0f, 8.0f,
            // Row 2
            9.0f, 10.0f, 11.0f, 12.0f
        };
        ReadOnlySpan<nint> shape = [1, 3, 4]; // NHW: batch=1, height=3, width=4
        var tensor = NumericsTensor.Create(tensorData, shape);

        // Act
        var result = TensorTestUtils.ExtractProbabilityMapsForTesting(tensor);

        // Assert
        Assert.Single(result);
        Assert.Equal(3, result[0].GetLength(0)); // height
        Assert.Equal(4, result[0].GetLength(1)); // width

        // Verify row-major order is preserved
        Assert.Equal(1.0f, result[0][0, 0]);
        Assert.Equal(4.0f, result[0][0, 3]);
        Assert.Equal(5.0f, result[0][1, 0]);
        Assert.Equal(12.0f, result[0][2, 3]);
    }

    [Fact]
    public void ExtractProbabilityMaps_BatchSizes_HandlesDifferentDimensions()
    {
        // Arrange: Create a 3D NHW Tensor<float> with larger batch/dimensions
        var tensorData = new float[]
        {
            // Batch 0: 2x3 image
            1.0f, 2.0f, 3.0f,
            4.0f, 5.0f, 6.0f,
            // Batch 1: 2x3 image
            7.0f, 8.0f, 9.0f,
            10.0f, 11.0f, 12.0f,
            // Batch 2: 2x3 image
            13.0f, 14.0f, 15.0f,
            16.0f, 17.0f, 18.0f
        };
        ReadOnlySpan<nint> shape = [3, 2, 3]; // NHW: batch=3, height=2, width=3
        var tensor = NumericsTensor.Create(tensorData, shape);

        // Act
        var result = TensorTestUtils.ExtractProbabilityMapsForTesting(tensor);

        // Assert
        Assert.Equal(3, result.Length); // 3 batches

        // Verify each batch has correct dimensions
        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(2, result[i].GetLength(0)); // height
            Assert.Equal(3, result[i].GetLength(1)); // width
        }

        // Verify specific values
        Assert.Equal(1.0f, result[0][0, 0]);   // Batch 0, top-left
        Assert.Equal(6.0f, result[0][1, 2]);   // Batch 0, bottom-right
        Assert.Equal(7.0f, result[1][0, 0]);   // Batch 1, top-left
        Assert.Equal(18.0f, result[2][1, 2]);  // Batch 2, bottom-right
    }

    [Fact]
    public void NhwcToNchw_SingleBatchSingleChannel_ConvertsCorrectly()
    {
        // Arrange: 2x3x1 tensor (H=2, W=3, C=1)
        var data = new float[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f };
        nint[] shape = [2, 3, 1]; // HWC shape

        // Act
        TensorOps.NhwcToNchw(data, shape);

        // Assert - Data should remain the same for single channel
        var expected = new float[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f };
        Assert.Equal(expected, data);
    }

    [Fact]
    public void NhwcToNchw_SingleBatchMultipleChannels_ReordersCorrectly()
    {
        // Arrange: 2x2x3 tensor (H=2, W=2, C=3)
        // HWC layout: [height][width][channel]
        var data = new float[]
        {
            // Pixel (0,0): R=1, G=2, B=3
            1.0f, 2.0f, 3.0f,
            // Pixel (0,1): R=4, G=5, B=6  
            4.0f, 5.0f, 6.0f,
            // Pixel (1,0): R=7, G=8, B=9
            7.0f, 8.0f, 9.0f,
            // Pixel (1,1): R=10, G=11, B=12
            10.0f, 11.0f, 12.0f
        };

        nint[] shape = [2, 2, 3]; // HWC shape

        // Act
        TensorOps.NhwcToNchw(data, shape);

        // Assert
        // CHW layout should be: [channel][height][width]
        // Red channel (all R values), then Green channel (all G values), then Blue channel (all B values)
        var expected = new float[]
        {
            // Red channel (C=0)
            1.0f, 4.0f,    // Row 0: pixels (0,0) and (0,1)
            7.0f, 10.0f,   // Row 1: pixels (1,0) and (1,1)
            // Green channel (C=1)  
            2.0f, 5.0f,    // Row 0: pixels (0,0) and (0,1)
            8.0f, 11.0f,   // Row 1: pixels (1,0) and (1,1)
            // Blue channel (C=2)
            3.0f, 6.0f,    // Row 0: pixels (0,0) and (0,1)
            9.0f, 12.0f    // Row 1: pixels (1,0) and (1,1)
        };

        Assert.Equal(expected, data);
    }



    [Fact]
    public void NhwcToNchw_InvalidDimensions_ThrowsArgumentException()
    {
        // Test 4D tensor (old format)
        var data4D = new float[6];
        Assert.Throws<ArgumentException>(() => TensorOps.NhwcToNchw(data4D, [1, 2, 3, 1]));

        // Test 5D tensor
        var data5D = new float[6];
        Assert.Throws<ArgumentException>(() => TensorOps.NhwcToNchw(data5D, [1, 1, 1, 2, 3]));

        // Test 2D tensor
        var data2D = new float[6];
        Assert.Throws<ArgumentException>(() => TensorOps.NhwcToNchw(data2D, [2, 3]));
    }

    [Fact]
    public void NhwcToNchw_EdgeCases_HandlesCorrectly()
    {
        // Test with 1x1x1 tensor (minimal valid case)
        var data1x1 = new float[] { 42.0f };
        nint[] shape1x1 = [1, 1, 1]; // HWC shape

        TensorOps.NhwcToNchw(data1x1, shape1x1);

        Assert.Equal(42.0f, data1x1[0]);

        // Test with large channel count
        var dataManyChannels = new float[32]; // 2x2x8 = 32 elements 
        for (int i = 0; i < 32; i++)
        {
            dataManyChannels[i] = i;
        }
        nint[] shapeManyChannels = [2, 2, 8]; // HWC: H=2, W=2, C=8

        TensorOps.NhwcToNchw(dataManyChannels, shapeManyChannels);

        // Verify data is still present (though reordered)
        var sortedOriginal = Enumerable.Range(0, 32).Select(i => (float)i).OrderBy(x => x).ToArray();
        var sortedResult = dataManyChannels.OrderBy(x => x).ToArray();
        Assert.Equal(sortedOriginal, sortedResult);
    }
}
