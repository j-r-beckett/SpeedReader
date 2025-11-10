// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Ocr.InferenceEngine;

namespace Ocr;

public record DetectionOptions
{
    public int TileWidth { get; init; } = 640;
    public int TileHeight { get; init; } = 640;
}

public record RecognitionOptions
{
    public int RecognitionInputWidth { get; init; } = 160;
    public int RecognitionInputHeight { get; init; } = 48;
}

public record OcrPipelineOptions
{
    public required DetectionOptions DetectionOptions { get; init; }
    public required RecognitionOptions RecognitionOptions { get; init; }

    public required CpuEngineConfig DetectionEngine { get; init; }
    public required CpuEngineConfig RecognitionEngine { get; init; }
}
