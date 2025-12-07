// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

namespace Native;

public class OrtValue
{
    public Memory<float> Data { get; }
    public int[] Shape { get; }

    private OrtValue(Memory<float> data, int[] shape)
    {
        Data = data;
        Shape = shape;
    }

    public static OrtValue Create(Memory<float> data, int[] shape)
    {
        ArgumentNullException.ThrowIfNull(shape);

        var expectedSize = shape.Aggregate(1, (a, b) => a * b);

        if (data.Length != expectedSize)
        {
            throw new ArgumentException(
                $"Data length {data.Length} does not match shape [{string.Join(", ", shape)}] (expected {expectedSize} elements)");
        }

        // Defensive copy of shape
        return new OrtValue(data, [.. shape]);
    }
}
