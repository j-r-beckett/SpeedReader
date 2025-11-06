// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Collections.Immutable;

namespace Ocr.Geometry;

public record ConvexHull
{
    // No JsonPropertyName, this record is for internal use only
    public required ImmutableList<Point> Points { get; init; }
}
