// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics.Metrics;

namespace Experimental;

public static class Telemetry
{
    private const string MetricNamespace = "SpeedReader";

    // Detection
    private static readonly Meter _detectionMeter = new($"{MetricNamespace}.Dbnet");

    public static readonly Gauge<double> DbnetInferenceDurationGauge =
        _detectionMeter.CreateGauge<double>("InferenceDuration", unit: "ms", "Duration of DBNet inference");

    public static readonly Gauge<double> DbnetInferenceThroughputGauge =
        _detectionMeter.CreateGauge<double>("InferenceThroughput", unit: "tiles/sec", description: "DBNet inference throughput");

    public static readonly Histogram<int> TileCountHistogram =
        _detectionMeter.CreateHistogram<int>("TileCount", description: "Number of tiles the image was split into");

    // Dimensions: num tiles
    public static readonly Gauge<double> DetectionDurationGauge =
        _detectionMeter.CreateGauge<double>("DetectionDuration", unit: "ms",
            description: "Duration of detection in milliseconds");

    // Dimensions: num tiles
    public static readonly Gauge<double> DetectionThroughputGauge =
        _detectionMeter.CreateGauge<double>("DetectionThroughput", unit: "images/sec",
            description: "Detection throughput");

    // Recognition
    private static readonly Meter _recognitionMeter = new($"{MetricNamespace}.Svtr");

    public static readonly Gauge<double> SvtrInferenceDurationGauge =
        _recognitionMeter.CreateGauge<double>("InferenceDuration", unit: "ms", description: "Duration of SVTR inference in milliseconds");

    // Application
    private static readonly Meter _applicationMeter = new($"{MetricNamespace}.Application");
}
