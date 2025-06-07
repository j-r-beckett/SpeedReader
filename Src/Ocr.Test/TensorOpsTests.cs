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
        using var buffer = CreateBufferFromTensor(tensor);
        var result = TensorTestUtils.ExtractProbabilityMapsForTesting(buffer);

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
        using var buffer = CreateBufferFromTensor(tensor);
        var result = TensorTestUtils.ExtractProbabilityMapsForTesting(buffer);

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
        using var buffer = CreateBufferFromTensor(tensor);
        var result = TensorTestUtils.ExtractProbabilityMapsForTesting(buffer);

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
        using var buffer = CreateBufferFromTensor(tensor);
        var result = TensorTestUtils.ExtractProbabilityMapsForTesting(buffer);

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
        // Arrange: 1x2x3x1 tensor (N=1, H=2, W=3, C=1)
        var data = new float[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f };
        var buffer = new Buffer<float>(data.Length, [1, 2, 3, 1]);
        data.CopyTo(buffer.AsSpan());

        // Act
        TensorOps.NhwcToNchw(buffer);

        // Assert
        Assert.Equal([1, 1, 2, 3], buffer.Shape); // NCHW shape

        // Data should remain the same for single channel
        var expected = new float[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f };
        Assert.Equal(expected, buffer.AsSpan().ToArray());
    }

    [Fact]
    public void NhwcToNchw_SingleBatchMultipleChannels_ReordersCorrectly()
    {
        // Arrange: 1x2x2x3 tensor (N=1, H=2, W=2, C=3)
        // NHWC layout: [batch][height][width][channel]
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

        var buffer = new Buffer<float>(data.Length, [1, 2, 2, 3]);
        data.CopyTo(buffer.AsSpan());

        // Act
        TensorOps.NhwcToNchw(buffer);

        // Assert
        Assert.Equal([1, 3, 2, 2], buffer.Shape); // NCHW shape

        // NCHW layout should be: [batch][channel][height][width]
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

        Assert.Equal(expected, buffer.AsSpan().ToArray());
    }

    [Fact]
    public void NhwcToNchw_MultipleBatches_ProcessesEachBatchIndependently()
    {
        // Arrange: 2x1x2x2 tensor (N=2, H=1, W=2, C=2)
        var data = new float[]
        {
            // Batch 0: 1x2x2 image with 2 channels
            1.0f, 2.0f,   // Pixel (0,0): C0=1, C1=2
            3.0f, 4.0f,   // Pixel (0,1): C0=3, C1=4
            
            // Batch 1: 1x2x2 image with 2 channels  
            5.0f, 6.0f,   // Pixel (0,0): C0=5, C1=6
            7.0f, 8.0f    // Pixel (0,1): C0=7, C1=8
        };

        var buffer = new Buffer<float>(data.Length, [2, 1, 2, 2]);
        data.CopyTo(buffer.AsSpan());

        // Act
        TensorOps.NhwcToNchw(buffer);

        // Assert
        Assert.Equal([2, 2, 1, 2], buffer.Shape); // NCHW shape

        var expected = new float[]
        {
            // Batch 0
            1.0f, 3.0f,   // Channel 0
            2.0f, 4.0f,   // Channel 1
            
            // Batch 1
            5.0f, 7.0f,   // Channel 0
            6.0f, 8.0f    // Channel 1
        };

        Assert.Equal(expected, buffer.AsSpan().ToArray());
    }

    [Fact]
    public void NhwcToNchw_LargerTensor_MaintainsDataIntegrity()
    {
        // Arrange: 1x3x4x2 tensor (N=1, H=3, W=4, C=2)
        var data = new float[24]; // 1*3*4*2 = 24 elements

        // Fill with incrementing values for easy verification
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = i + 1.0f;
        }

        var buffer = new Buffer<float>(data.Length, [1, 3, 4, 2]);
        data.CopyTo(buffer.AsSpan());

        // Act
        TensorOps.NhwcToNchw(buffer);

        // Assert
        Assert.Equal([1, 2, 3, 4], buffer.Shape); // NCHW shape

        // Verify that all original values are present
        var result = buffer.AsSpan().ToArray();
        Array.Sort(result);
        Array.Sort(data);
        Assert.Equal(data, result);

        // Verify specific channel groupings
        var actualBuffer = buffer.AsSpan();
        var tensor = buffer.AsTensor();

        // Check that channel 0 contains all the even-indexed original values
        // Check that channel 1 contains all the odd-indexed original values
        for (int h = 0; h < 3; h++)
        {
            for (int w = 0; w < 4; w++)
            {
                // Original NHWC index calculation: n*H*W*C + h*W*C + w*C + c
                int originalC0Index = 0 * 3 * 4 * 2 + h * 4 * 2 + w * 2 + 0;
                int originalC1Index = 0 * 3 * 4 * 2 + h * 4 * 2 + w * 2 + 1;

                float expectedC0Value = originalC0Index + 1.0f;
                float expectedC1Value = originalC1Index + 1.0f;

                Assert.Equal(expectedC0Value, tensor[[0, 0, h, w]]); // Channel 0
                Assert.Equal(expectedC1Value, tensor[[0, 1, h, w]]); // Channel 1
            }
        }
    }

    [Fact]
    public void NhwcToNchw_InvalidDimensions_ThrowsArgumentException()
    {
        // Test 3D tensor
        var buffer3D = new Buffer<float>(6, [2, 3, 1]);
        Assert.Throws<ArgumentException>(() => TensorOps.NhwcToNchw(buffer3D));

        // Test 5D tensor
        var buffer5D = new Buffer<float>(6, [1, 1, 1, 2, 3]);
        Assert.Throws<ArgumentException>(() => TensorOps.NhwcToNchw(buffer5D));

        // Test 2D tensor
        var buffer2D = new Buffer<float>(6, [2, 3]);
        Assert.Throws<ArgumentException>(() => TensorOps.NhwcToNchw(buffer2D));
    }

    [Fact]
    public void NhwcToNchw_EdgeCases_HandlesCorrectly()
    {
        // Test with 1x1x1x1 tensor (minimal valid case)
        var buffer1x1 = new Buffer<float>(1, [1, 1, 1, 1]);
        buffer1x1.AsSpan()[0] = 42.0f;

        TensorOps.NhwcToNchw(buffer1x1);

        Assert.Equal([1, 1, 1, 1], buffer1x1.Shape);
        Assert.Equal(42.0f, buffer1x1.AsSpan()[0]);

        // Test with large channel count
        var bufferManyChannels = new Buffer<float>(32, [1, 2, 2, 8]); // 8 channels
        for (int i = 0; i < 32; i++)
        {
            bufferManyChannels.AsSpan()[i] = i;
        }

        TensorOps.NhwcToNchw(bufferManyChannels);

        Assert.Equal([1, 8, 2, 2], bufferManyChannels.Shape);

        // Verify data is still present (though reordered)
        var sortedOriginal = Enumerable.Range(0, 32).Select(i => (float)i).OrderBy(x => x).ToArray();
        var sortedResult = bufferManyChannels.AsSpan().ToArray().OrderBy(x => x).ToArray();
        Assert.Equal(sortedOriginal, sortedResult);
    }

    [Fact]
    public void NhwcToNchw_IntegerTypes_WorksWithDifferentDataTypes()
    {
        // Test with byte data (common for image processing)
        var byteData = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var byteBuffer = new Buffer<byte>(byteData.Length, [1, 2, 2, 2]);
        byteData.CopyTo(byteBuffer.AsSpan());

        TensorOps.NhwcToNchw(byteBuffer);

        Assert.Equal([1, 2, 2, 2], byteBuffer.Shape);

        // Test with int data
        var intData = new int[] { 10, 20, 30, 40 };
        var intBuffer = new Buffer<int>(intData.Length, [1, 1, 2, 2]);
        intData.CopyTo(intBuffer.AsSpan());

        TensorOps.NhwcToNchw(intBuffer);

        Assert.Equal([1, 2, 1, 2], intBuffer.Shape);

        // Verify the reordering worked correctly for integers
        var intTensor = intBuffer.AsTensor();
        Assert.Equal(10, intTensor[[0, 0, 0, 0]]); // First channel, position (0,0)
        Assert.Equal(30, intTensor[[0, 0, 0, 1]]); // First channel, position (0,1)
        Assert.Equal(20, intTensor[[0, 1, 0, 0]]); // Second channel, position (0,0)
        Assert.Equal(40, intTensor[[0, 1, 0, 1]]); // Second channel, position (0,1)
    }

    private static Buffer<float> CreateBufferFromTensor(System.Numerics.Tensors.Tensor<float> tensor)
    {
        var buffer = new Buffer<float>((int)tensor.FlattenedLength, tensor.Lengths.ToArray());
        tensor.FlattenTo(buffer.AsSpan());
        return buffer;
    }
}
