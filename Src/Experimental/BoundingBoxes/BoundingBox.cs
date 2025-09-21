// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Text.Json.Serialization;

namespace Experimental.BoundingBoxes;

public record BoundingBox
{
    [JsonPropertyName("polygon")]
    public Polygon Polygon { get; set; }

    [JsonPropertyName("rotatedRectangle")]
    public RotatedRectangle RotatedRectangle { get; set; }

    [JsonPropertyName("rectangle")]
    public AxisAlignedRectangle AxisAlignedRectangle { get; set; }

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
