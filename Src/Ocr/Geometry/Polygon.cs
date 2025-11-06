// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Ocr.Geometry;

public record Polygon
{
    [JsonPropertyName("points")]
    public required ImmutableList<Point> Points { get; init; }
}

public static partial class PolygonExtensions
{
    public static RotatedRectangle ToRotatedRectangle(this Polygon polygon) =>
        polygon.ToConvexHull().ToRotatedRectangle();
}
