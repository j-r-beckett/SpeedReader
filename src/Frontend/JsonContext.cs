// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Text.Json.Serialization;
using Ocr.Geometry;

namespace Frontend;

[JsonSerializable(typeof(OcrJsonResult))]
[JsonSerializable(typeof(List<OcrJsonResult>))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(BoundingBox))]
[JsonSerializable(typeof(Polygon))]
[JsonSerializable(typeof(RotatedRectangle))]
[JsonSerializable(typeof(AxisAlignedRectangle))]
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class JsonContext : JsonSerializerContext
{
}

public record OcrJsonResult(
    string? Filename,
    List<OcrTextResult> Results);

public record OcrTextResult(
    BoundingBox BoundingBox,
    string Text,
    double Confidence);

public record ErrorResponse(string Error);
