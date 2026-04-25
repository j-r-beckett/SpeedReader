// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Text.Json.Serialization;
using SpeedReader.Ocr;
using SpeedReader.Ocr.Geometry;

namespace SpeedReader.Library;

[JsonSerializable(typeof(OcrJsonResult))]
[JsonSerializable(typeof(BoundingBox))]
[JsonSerializable(typeof(Polygon))]
[JsonSerializable(typeof(RotatedRectangle))]
[JsonSerializable(typeof(AxisAlignedRectangle))]
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class LibraryJsonContext : JsonSerializerContext
{
}
