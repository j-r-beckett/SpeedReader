// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Experimental.BoundingBoxes;

public record RotatedRectangle
{
    [JsonPropertyName("x")]
    public required int X { get; set; }  // Top left x

    [JsonPropertyName("y")]
    public required int Y { get; set; }  // Top left y

    [JsonPropertyName("height")]
    public required double Height  // Height
    {
        get;
        set => field = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    [JsonPropertyName("width")]
    public required double Width  // Width
    {
        get;
        set => field = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    [JsonPropertyName("angle")]
    public required double Angle { get; init; }  // Angle in radians
}

public static partial class RotatedRectangleExtensions
{
    public static Polygon ToPolygon(this RotatedRectangle rotatedRectangle) =>
        new() { Points = rotatedRectangle.Corners().Select(p => (Point)p).ToList() };

    public static List<PointF> Corners(this RotatedRectangle rotatedRectangle)
    {
        var cos = Math.Cos(rotatedRectangle.Angle);
        var sin = Math.Sin(rotatedRectangle.Angle);

        var topLeft = new PointF
        {
            X = rotatedRectangle.X,
            Y = rotatedRectangle.Y
        };

        var topRight = new PointF
        {
            X = rotatedRectangle.X + rotatedRectangle.Width * cos,
            Y = rotatedRectangle.Y + rotatedRectangle.Width * sin
        };

        var bottomRight = new PointF
        {
            X = rotatedRectangle.X + rotatedRectangle.Width * cos - rotatedRectangle.Height * sin,
            Y = rotatedRectangle.Y + rotatedRectangle.Width * sin + rotatedRectangle.Height * cos
        };

        var bottomLeft = new PointF
        {
            X = rotatedRectangle.X - rotatedRectangle.Height * sin,
            Y = rotatedRectangle.Y + rotatedRectangle.Height * cos
        };

        return [topLeft, topRight, bottomRight, bottomLeft];  // clockwise order
    }
}
