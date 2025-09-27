// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Numerics.Tensors;

namespace Experimental.Algorithms;

public static partial class ReliefMapExtensions
{
    public static void Binarize(this ReliefMap map, float threshold)
    {
        Tensor.Subtract(map.Data, threshold, map.Data);
        Tensor.Ceiling<float>(map.Data, map.Data);
    }
}
