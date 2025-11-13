// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Text.Json.Serialization;

namespace Ocr.Telemetry;

[JsonSerializable(typeof(Dictionary<string, string>))]
internal partial class MetricJsonContext : JsonSerializerContext
{
}
