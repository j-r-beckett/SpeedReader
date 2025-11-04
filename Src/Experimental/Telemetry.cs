// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Resources;

namespace Experimental;

public static class Telemetry
{
    private const string MetricNamespace = "SpeedReader";

    #region Inference
    // All instruments should have model, quantization dimensions
    private static readonly Meter _inferenceMeter = new($"{MetricNamespace}.Inference");

    public static readonly Histogram<double> InferenceDurationHistogram =
        _inferenceMeter.CreateHistogram<double>("InferenceDuration", "ms", "Inference duration for a single item");

    public static readonly Histogram<double> InferenceThroughputHistogram =
        _inferenceMeter.CreateHistogram<double>("InferenceThroughput", "items/sec", description: "Inference throughput");

    public static readonly Histogram<double> InferenceParallelismHistogram =
        _inferenceMeter.CreateHistogram<double>("InferenceParallelism", "threads",
            description: "Observed number of concurrent inference threads");

    public static readonly Histogram<int> InferenceMaxParallelismHistogram =
        _inferenceMeter.CreateHistogram<int>("InferenceMaxParallelism", "threads",
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

public class InferenceTelemetryRecorder
{
    private readonly TagList _tags;

    public InferenceTelemetryRecorder(Model model, ModelPrecision precision)
    {
        _tags =
        [
            new KeyValuePair<string, object?>("model", model.ToString()),
            new KeyValuePair<string, object?>("precision", precision.ToString())
        ];
    }

    public void RecordDuration(TimeSpan duration) => Telemetry.InferenceDurationHistogram.Record(duration.TotalMilliseconds, _tags);

    public void RecordThroughput(double throughput) => Telemetry.InferenceThroughputHistogram.Record(throughput, _tags);

    public void RecordParallelism(double parallelism) => Telemetry.InferenceParallelismHistogram.Record(parallelism, _tags);

    public void RecordMaxParallelism(int maxParallelism) => Telemetry.InferenceMaxParallelismHistogram.Record(maxParallelism, _tags);
}
