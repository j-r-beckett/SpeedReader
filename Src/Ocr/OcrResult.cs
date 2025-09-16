// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Text.Json.Serialization;

namespace Ocr
{
    public record OcrResult
    {
        [JsonPropertyName("pageNumber")]
        public int PageNumber
        {
            get; init;
        }

        [JsonPropertyName("blocks")]
        public List<Block> Blocks { get; init; } = new();

        [JsonPropertyName("lines")]
        public List<Line> Lines { get; init; } = new();

        [JsonPropertyName("words")]
        public List<Word> Words { get; init; } = new();
    }

    public record BoundingBox
    {
        [JsonPropertyName("polygon")]
        public List<JsonPoint> Polygon { get; init; } = new();

        [JsonPropertyName("aarectangle")]
        public required AARectangle AARectangle
        {
            get; init;
        }

        [JsonPropertyName("orectangle")]
        public List<JsonPoint> ORectangle { get; init; } = new();
    }

    public record JsonPoint
    {
        [JsonPropertyName("x")]
        public double X
        {
            get; init;
        }

        [JsonPropertyName("y")]
        public double Y
        {
            get; init;
        }
    }

    public record AARectangle
    {
        [JsonPropertyName("x")]
        public double X
        {
            get; init;
        }

        [JsonPropertyName("y")]
        public double Y
        {
            get; init;
        }

        [JsonPropertyName("width")]
        public double Width
        {
            get; init;
        }

        [JsonPropertyName("height")]
        public double Height
        {
            get; init;
        }
    }

    public record Block
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("boundingBox")]
        public required BoundingBox BoundingBox
        {
            get; init;
        }

        [JsonPropertyName("confidence")]
        public double Confidence
        {
            get; init;
        }

        [JsonPropertyName("lineIds")]
        public List<string> LineIds { get; init; } = new();
    }

    public record Line
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("boundingBox")]
        public required BoundingBox BoundingBox
        {
            get; init;
        }

        [JsonPropertyName("confidence")]
        public double Confidence
        {
            get; init;
        }

        [JsonPropertyName("text")]
        public string Text { get; init; } = string.Empty;

        [JsonPropertyName("wordIds")]
        public List<string> WordIds { get; init; } = new();
    }

    public record Word
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("boundingBox")]
        public required BoundingBox BoundingBox
        {
            get; init;
        }

        [JsonPropertyName("confidence")]
        public double Confidence
        {
            get; init;
        }

        [JsonPropertyName("text")]
        public string Text { get; init; } = string.Empty;
    }
}
