// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Text.Json.Serialization;

namespace Experimental.Geometry;

public record BoundingBox
{
    [JsonPropertyName("polygon")]
    public Polygon Polygon { get; }

    [JsonPropertyName("rotatedRectangle")]
    public RotatedRectangle RotatedRectangle { get; }

    [JsonPropertyName("rectangle")]
    public AxisAlignedRectangle AxisAlignedRectangle { get; }

    public BoundingBox(Polygon polygon, RotatedRectangle rotatedRectangle, AxisAlignedRectangle axisAlignedRectangle)
    {
        Polygon = polygon;
        RotatedRectangle = rotatedRectangle;
        AxisAlignedRectangle = axisAlignedRectangle;
    }

    public BoundingBox(Polygon polygon, RotatedRectangle rotatedRectangle)
        : this(polygon, rotatedRectangle, rotatedRectangle.ToAxisAlignedRectangle()) { }

    public BoundingBox(Polygon polygon) : this(polygon, polygon.ToRotatedRectangle()) { }
}
