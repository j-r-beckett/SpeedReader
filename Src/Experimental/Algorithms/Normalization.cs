// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Numerics;

namespace Experimental.Algorithms;

public static class Normalization
{
    public static void Normalize(this float[] tensor, float mean, float std) => Normalize(tensor.AsSpan(), mean, std);

    public static void Normalize(this float[] tensor, ReadOnlySpan<float> means, ReadOnlySpan<float> stds)
    {
        if (means.Length != stds.Length)
            throw new ArgumentException("Mean and standard deviation must have the same length");

        if (tensor.Length % means.Length != 0)
            throw new ArgumentException("Tensor length must be a multiple of the number of means/stds");

        var tensorSpan = tensor.AsSpan();

        var numSlices = means.Length;
        var sliceSize = tensor.Length / numSlices;

        for (int i = 0; i < numSlices; i++)
        {
            Normalize(tensorSpan.Slice(i * sliceSize, sliceSize), means[i], stds[i]);
        }
    }

    private static void Normalize(Span<float> tensor, float mean, float std)
    {
        int i = 0;
        if (Vector.IsHardwareAccelerated && tensor.Length >= Vector<float>.Count)
        {
            var meanVec = new Vector<float>(mean);
            var stdVec = new Vector<float>(std);
            var vectorSize = Vector<float>.Count;
            for (; i <= tensor.Length - vectorSize; i += vectorSize)
            {
                Vector<float> vec = new(tensor.Slice(i, vectorSize));
                vec = (vec - meanVec) / stdVec;
                vec.CopyTo(tensor.Slice(i, vectorSize));
            }
        }

        for (; i < tensor.Length; i++)
        {
            tensor[i] = (tensor[i] - mean) / std;
        }
    }
}
