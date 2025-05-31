using Microsoft.ML.OnnxRuntime;

namespace TextDetection;

public class PostProcessor
{
    public static float[][,] PostProcess(OrtValue tensor)
    {
        var outputSpan = tensor.GetTensorMutableDataAsSpan<float>();
        var shape = tensor.GetTensorTypeAndShape().Shape;

        // Output shape is [batch_size, height, width]
        int batchSize = (int)shape[0];
        int height = (int)shape[1];
        int width = (int)shape[2];

        var results = new float[batchSize][,];
        int imageSize = height * width;

        for (int batchIndex = 0; batchIndex < batchSize; batchIndex++)
        {
            var probabilityMap = new float[height, width];
            int batchOffset = batchIndex * imageSize;

            for (int h = 0; h < height; h++)
            {
                for (int w = 0; w < width; w++)
                {
                    probabilityMap[h, w] = outputSpan[batchOffset + h * width + w];
                }
            }

            results[batchIndex] = probabilityMap;
        }

        return results;
    }

    public static bool[,] BinarizeProbabilityMap(float[,] probabilityMap)
    {
        int height = probabilityMap.GetLength(0);
        int width = probabilityMap.GetLength(1);
        var binaryMap = new bool[height, width];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                binaryMap[y, x] = probabilityMap[y, x] > 0.2f;
            }
        }

        return binaryMap;
    }
}
