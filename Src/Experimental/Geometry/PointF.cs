// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Text.Json.Serialization;

namespace Experimental.Geometry;

public record PointF
{
    [JsonPropertyName("x")]
    public required double X { get; set; }

    [JsonPropertyName("y")]
    public required double Y { get; set; }

    // No loss in precision
    public static implicit operator PointF(Point point) => new() { X = point.X, Y = point.Y };

    public static implicit operator PointF((double X, double Y) point) => new() { X = point.X, Y = point.Y };

    public void Deconstruct(out double x, out double y)
    {
        x = X;
        y = Y;
    }
}
