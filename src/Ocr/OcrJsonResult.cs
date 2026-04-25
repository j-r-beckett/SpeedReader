// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using SpeedReader.Ocr.Geometry;

namespace SpeedReader.Ocr;

public record OcrJsonResult(
    string? Filename,
    List<OcrTextResult> Results);

public record OcrTextResult(
    BoundingBox BoundingBox,
    string Text,
    double Confidence);
