// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using System.Numerics;

namespace Experimental.Algorithms;

public static partial class ReliefMapExtensions
{
    public static void Binarize(this ReliefMap map, float threshold)
    {
        Debug.Assert(map.Data.All(x => x >= 0.0f && x <= 1.0f));

        var data = map.Data.AsSpan();

        var vectorSize = Vector<float>.Count;

        int i = 0;
        if (Vector.IsHardwareAccelerated && data.Length >= vectorSize)
        {
            var thresholdVec = new Vector<float>(float.BitDecrement(threshold));
            for (; i <= data.Length - vectorSize; i += vectorSize)
            {
                Vector<float> vec = new(data.Slice(i, vectorSize));
                vec -= thresholdVec;
                vec = Vector.Ceiling(vec);
                vec.CopyTo(data.Slice(i, vectorSize));
            }
        }

        for ( ; i < data.Length; i++)
            data[i] = data[i] >= threshold ? 1.0f : 0.0f;
    }
}
