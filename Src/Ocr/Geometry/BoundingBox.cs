// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Text.Json.Serialization;

namespace Ocr.Geometry;

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

    public static BoundingBox? Create(Polygon polygon)
    {
        var convexHull = polygon.ToConvexHull();
        if (convexHull.Points.Count < 3)
            return null;

        var rotatedRectangle = convexHull.ToRotatedRectangle();
        return new BoundingBox(polygon, rotatedRectangle);
    }
}
