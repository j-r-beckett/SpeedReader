// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Text.Json.Serialization;

namespace Experimental.BoundingBoxes;

public record AxisAlignedRectangle
{
    [JsonPropertyName("x")]
    public required int X  // Top left x
    {
        get;
        set => field = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }


    [JsonPropertyName("y")]
    public required int Y  // Top left y
    {
        get;
        set => field = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    [JsonPropertyName("height")]
    public required int Height  // Height in pixels
    {
        get;
        set => field = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    [JsonPropertyName("width")]
    public required int Width  // Width in pixels
    {
        get;
        set => field = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }
}

public static class AxisAlignedRectangleExtensions
{
    public static List<Point> Corners(this AxisAlignedRectangle rectangle) => [
            new Point { X = rectangle.X, Y = rectangle.Y },
            new Point { X = rectangle.X + rectangle.Width, Y = rectangle.Y },
            new Point { X = rectangle.X + rectangle.Width, Y = rectangle.Y + rectangle.Height },
            new Point { X = rectangle.X, Y = rectangle.Y + rectangle.Height }
        ];
}
