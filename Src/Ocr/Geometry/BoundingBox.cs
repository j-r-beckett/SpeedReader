// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Text.Json.Serialization;

namespace Ocr.Geometry;

public record BoundingBox
{
    [JsonPropertyName("polygon")]
    public required Polygon Polygon { get; init; }

    [JsonPropertyName("rotatedRectangle")]
    public required RotatedRectangle RotatedRectangle { get; init; }

    [JsonPropertyName("rectangle")]
    public required AxisAlignedRectangle AxisAlignedRectangle { get; init; }
}
