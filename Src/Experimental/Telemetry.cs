// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics.Metrics;

namespace Experimental;

public static class Telemetry
{
    private const string MetricNamespace = "SpeedReader";

    #region Inference
    // All instruments should have model, quantization dimensions
    private static readonly Meter _inferenceMeter = new($"{MetricNamespace}.Inference");

    public static readonly Gauge<double> InferenceDurationGauge =
        _inferenceMeter.CreateGauge<double>("InferenceDuration", "ms", "Inference duration for a single item");

    public static readonly Gauge<double> InferenceThroughputGauge =
        _inferenceMeter.CreateGauge<double>("InferenceThroughput", "items/sec", description: "Inference throughput");

    public static readonly Gauge<double> InferenceInstantaneousThroughputGauge =
        _inferenceMeter.CreateGauge<double>("InferenceInstantaneousThroughput", "items/sec", description: "Instantaneous inference throughput");

    public static readonly Gauge<double> InferenceParallelismGauge =
        _inferenceMeter.CreateGauge<double>("InferenceParallelism", "threads",
            description: "Observed number of concurrent inference threads");

    public static readonly Gauge<int> InferenceMaxParallelismGauge =
        _inferenceMeter.CreateGauge<int>("InferenceMaxParallelism", "threads",
            description: "Maximum number of concurrent inference threads");
    #endregion

    #region Application
    private static readonly Meter _applicationMeter = new($"{MetricNamespace}.Application");

    public static readonly Histogram<int> TileCountHistogram =
        _applicationMeter.CreateHistogram<int>("TileCount", description: "Number of tiles the image was split into");

    // Dimensions: num tiles
    public static readonly Gauge<double> DetectionDurationGauge =
        _applicationMeter.CreateGauge<double>("DetectionDuration", "ms",
            description: "Duration of detection for a full image");

    // Dimensions: num tiles
    public static readonly Gauge<double> DetectionThroughputGauge =
        _applicationMeter.CreateGauge<double>("DetectionThroughput", "images/sec",
            description: "Detection throughput for full images");

    // Dimensions: num words (bucketed by 100)
    public static readonly Gauge<double> RecognitionDurationGauge =
        _applicationMeter.CreateGauge<double>("DetectionDuration", "ms",
            description: "Duration of recognition for a full image");

    // Dimensions: num words (bucketed by 100)
    public static readonly Gauge<double> RecognitionThroughputGauge =
        _applicationMeter.CreateGauge<double>("DetectionThroughput", "images/sec",
            description: "Recognition throughput for full images");
    #endregion
}
