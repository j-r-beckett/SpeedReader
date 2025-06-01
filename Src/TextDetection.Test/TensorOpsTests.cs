using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Xunit;

namespace TextDetection.Test;

public class TensorOpsTests
{
    [Fact]
    public void ExtractProbabilityMaps_SingleBatch_ExtractsCorrectly()
    {
        // Arrange: Create a 1x2x2 tensor with known values
        var tensorData = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
        long[] shape = [1, 2, 2];
        using var tensor = OrtValue.CreateTensorValueFromMemory(tensorData, shape);

        // Act
        var result = TensorOps.ExtractProbabilityMaps(tensor);

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
        // Arrange: Create a 2x2x2 tensor (2 batches of 2x2 images)
        var tensorData = new float[] 
        {
            // Batch 0
            0.1f, 0.2f, 0.3f, 0.4f,
            // Batch 1  
            0.5f, 0.6f, 0.7f, 0.8f
        };
        long[] shape = [2, 2, 2];
        using var tensor = OrtValue.CreateTensorValueFromMemory(tensorData, shape);

        // Act
        var result = TensorOps.ExtractProbabilityMaps(tensor);

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
        // Arrange: Create a 1x3x4 tensor to test row/column ordering
        var tensorData = new float[] 
        {
            // Row 0
            1.0f, 2.0f, 3.0f, 4.0f,
            // Row 1
            5.0f, 6.0f, 7.0f, 8.0f,
            // Row 2
            9.0f, 10.0f, 11.0f, 12.0f
        };
        long[] shape = [1, 3, 4]; // batch=1, height=3, width=4
        using var tensor = OrtValue.CreateTensorValueFromMemory(tensorData, shape);

        // Act
        var result = TensorOps.ExtractProbabilityMaps(tensor);

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
}