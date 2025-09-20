// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Text.Json.Serialization;

namespace Experimental.BoundingBoxes;

public record Point
{
    [JsonPropertyName("x")]
    public required int X
    {
        get;
        set => field = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    [JsonPropertyName("y")]
    public required int Y
    {
        get;
        set => field = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    public void Deconstruct(out int x, out int y) { x = X; y = Y; }

    public static implicit operator Point((int X, int Y) point) => new() { X = point.X, Y = point.Y };
}
