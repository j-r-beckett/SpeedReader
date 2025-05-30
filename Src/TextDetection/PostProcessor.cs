using Microsoft.ML.OnnxRuntime;

namespace TextDetection;

public class PostProcessor
{
    public static float[][,] PostProcess(OrtValue tensor)
    {
        var outputSpan = tensor.GetTensorDataAsSpan<float>();
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
}