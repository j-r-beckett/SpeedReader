// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

namespace Experimental.Algorithms;

public static partial class ReliefMapExtensions
{
    public static void Binarize(this ReliefMap map, float threshold)
    {
        var data = map.Data;
        for (int i = 0; i < data.Length; i++)
            data[i] = data[i] >= threshold ? 1.0f : 0.0f;
    }
}
