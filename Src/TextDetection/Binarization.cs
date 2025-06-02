using System.Numerics.Tensors;
using CommunityToolkit.HighPerformance;

namespace TextDetection;

public static class Binarization
{
    // TODO: vectorize
    public static void BinarizeInPlace(Span2D<float> probabilityMap, float threshold)
    {
        // TensorPrimitives.Clamp<float>(probabilityMap, 1.0f, 2.0f);
        int height = probabilityMap.Height;
        int width = probabilityMap.Width;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                probabilityMap[y, x] = probabilityMap[y, x] > threshold ? 1.0f : 0.0f;
            }
        }
    }
}
