// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Text.Json.Serialization;

namespace Experimental;

public record Metadata
{
    [JsonPropertyName("width")]
    public required int Width  // Width in pixels
    {
        get;
        set => field = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    [JsonPropertyName("height")]
    public required int Height  // Height in pixels
    {
        get;
        set => field = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    [JsonPropertyName("id")]
    public string Id
    {
        get;
        set
        {
            const int maxIdLength = 2048;
            ArgumentNullException.ThrowIfNull(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value.Length, maxIdLength);
            field = value;
        }
    } = string.Empty;
}


public record OcrResult
{
    [JsonPropertyName("metadata")]
    public required Metadata Metadata;

    [JsonPropertyName("confidence")]
    public double Confidence { get; init; }

    [JsonPropertyName("text")]
    public string Text { get; init; } = string.Empty;
}
