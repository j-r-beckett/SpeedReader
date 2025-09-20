// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Text.Json.Serialization;

namespace Experimental.BoundingBoxes;

public record Polygon
{
    [JsonPropertyName("points")]
    public required List<Point> Points { get; set; }
}

public static partial class PolygonExtensions
{
    public static RotatedRectangle ToRotatedRectangle(this Polygon polygon) =>
        polygon.ToConvexHull().ToRotatedRectangle();
}
