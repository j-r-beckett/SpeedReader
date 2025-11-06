// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

namespace Ocr.Telemetry;

public readonly record struct MetricPoint(TimeSpan Timestamp, string Name, double Value, Dictionary<string, string>? Tags = null);
