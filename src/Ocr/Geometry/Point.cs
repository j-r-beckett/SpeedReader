// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Text.Json.Serialization;

namespace SpeedReader.Ocr.Geometry;

public readonly struct Point
{
    [JsonPropertyName("x")]
    public int X { get; init; }

    [JsonPropertyName("y")]
    public int Y { get; init; }

    // Loses precision, explicit cast
    public static explicit operator Point(PointF point) => new()
    {
        X = (int)Math.Round(point.X),
        Y = (int)Math.Round(point.Y)
    };

    public static implicit operator Point((int X, int Y) point) => new() { X = point.X, Y = point.Y };

    public void Deconstruct(out int x, out int y) { x = X; y = Y; }
}
