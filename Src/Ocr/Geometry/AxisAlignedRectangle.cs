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

    public Polygon Corners() => new([
        new PointF { X = X, Y = Y },                   // Top left
        new PointF { X = X + Width, Y = Y },           // Top right
        new PointF { X = X + Width, Y = Y + Height },  // Bottom right
        new PointF { X = X, Y = Y + Height }           // Bottom left
    ]);
}
