// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace Ocr.Geometry;

public record AxisAlignedRectangle
{
    [JsonPropertyName("x")]
    public required int X  // Top left x
    {
        get;
        init;
    }


    [JsonPropertyName("y")]
    public required int Y  // Top left y
    {
        get;
        init;
    }

    [JsonPropertyName("height")]
    public required int Height  // Height in pixels
    {
        get;
        init => field = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    [JsonPropertyName("width")]
    public required int Width  // Width in pixels
    {
        get;
        init => field = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }
}

public static class AxisAlignedRectangleExtensions
{
    public static Polygon ToPolygon(this AxisAlignedRectangle rectangle) =>
        new(new List<Point>
        {
            new() { X = rectangle.X, Y = rectangle.Y },
            new() { X = rectangle.X + rectangle.Width, Y = rectangle.Y },
            new() { X = rectangle.X + rectangle.Width, Y = rectangle.Y + rectangle.Height },
            new() { X = rectangle.X, Y = rectangle.Y + rectangle.Height }
        });
}
