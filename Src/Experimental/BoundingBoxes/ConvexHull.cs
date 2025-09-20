// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

namespace Experimental.BoundingBoxes;

public record ConvexHull
{
    // No JsonPropertyName, this record is for internal use only
    public required List<Point> Points { get; set; }
}
