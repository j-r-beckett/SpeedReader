using System.Diagnostics;
using System.Numerics.Tensors;

namespace OCR.Algorithms;

public static class Thresholding
{
    public static void BinarizeInPlace(Tensor<float> probabilityMap, float threshold)
    {
        Debug.Assert(probabilityMap.Min() >= 0 && probabilityMap.Max() <= 1);
        Tensor.Subtract(probabilityMap, threshold, probabilityMap);
        Tensor.Ceiling<float>(probabilityMap, probabilityMap);
    }
}
