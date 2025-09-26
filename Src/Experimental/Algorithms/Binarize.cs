// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using System.Numerics.Tensors;

namespace Experimental.Algorithms;

public static class Binarize
{
    public static void BinarizeInPlace(this float[] probabilityMap, float threshold)
    {
        Debug.Assert(probabilityMap.Min() >= 0 && probabilityMap.Max() <= 1);
        Tensor.Subtract(probabilityMap, threshold, probabilityMap);
        Tensor.Ceiling<float>(probabilityMap, probabilityMap);
    }
}
