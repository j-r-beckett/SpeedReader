// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Text.Json.Serialization;

namespace SpeedReader.Ocr.Geometry;

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

public static class RotatedRectangleExtensions
{
    public static AxisAlignedRectangle ToAxisAlignedRectangle(this RotatedRectangle rotatedRectangle)
    {
        var points = rotatedRectangle.Corners().Points;

        var maxX = double.NegativeInfinity;
        var maxY = double.NegativeInfinity;
        var minX = double.PositiveInfinity;
        var minY = double.PositiveInfinity;
        foreach (var point in points)
        {
            maxX = Math.Max(maxX, point.X);
            maxY = Math.Max(maxY, point.Y);
            minX = Math.Min(minX, point.X);
            minY = Math.Min(minY, point.Y);
        }

        return new AxisAlignedRectangle
        {
            X = minX,
            Y = minY,
            Width = maxX - minX,
            Height = maxY - minY
        };
    }
}
