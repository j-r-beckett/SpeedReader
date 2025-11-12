// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Text.Json.Serialization;

namespace Ocr.Geometry;

public record AxisAlignedRectangle
{
    [JsonPropertyName("x")]
    public required double X  // Top left x
    {
        get;
        init;
    }

    [JsonPropertyName("y")]
    public required double Y  // Top left y
    {
        get;
        init;
    }

    [JsonPropertyName("height")]
    public required double Height  // Height in pixels
    {
        get;
        init => field = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    [JsonPropertyName("width")]
    public required double Width  // Width in pixels
    {
        get;
        init => field = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    public Polygon ToPolygon() => new(new List<PointF>
    {
        new() { X = X, Y = Y },
        new() { X = X + Width, Y = Y },
        new() { X = X + Width, Y = Y + Height },
        new() { X = X, Y = Y + Height }
    });
}
