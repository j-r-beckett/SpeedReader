using System.Numerics.Tensors;
using CommunityToolkit.HighPerformance;

namespace OCR.Test;

public static class TensorTestUtils
{
    /// <summary>
    /// Extracts probability maps from NHW tensor format for testing purposes.
    /// Creates new arrays and copies data from spans.
    /// </summary>
    public static float[][,] ExtractProbabilityMapsForTesting(Buffer<float> buffer)
    {
        var tensor = buffer.AsTensor();
        var shape = tensor.Lengths;

        if (shape.Length != 3)
        {
            throw new ArgumentException($"Expected 3D [N,H,W] tensor, got {shape.Length}D tensor");
        }

        int batchSize = (int)shape[0];
        int height = (int)shape[1];
        int width = (int)shape[2];

        var tensorData = new float[tensor.FlattenedLength];
        tensor.FlattenTo(tensorData);

        var results = new float[batchSize][,];
        int imageSize = height * width;

        for (int batchIndex = 0; batchIndex < batchSize; batchIndex++)
        {
            int batchOffset = batchIndex * imageSize;
            var hwSpan = tensorData.AsSpan(batchOffset, imageSize);
            var span2D = hwSpan.AsSpan2D(height, width);

            // Copy from span to new array for testing
            var probabilityMap = new float[height, width];
            span2D.CopyTo(probabilityMap);

            results[batchIndex] = probabilityMap;
        }

        return results;
    }
}
