// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Collections.ObjectModel;

namespace Ocr.Geometry;

public record ConvexHull
{
    // No JsonPropertyName, this record is for internal use only
    public required ReadOnlyCollection<Point> Points { get; init; }
}
