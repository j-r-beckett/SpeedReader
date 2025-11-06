// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Resources;

namespace Experimental;

public static class TelemetryRecorder
{
    private const string MetricNamespace = "SpeedReader";

    #region Inference
    // All instruments should have model, quantization dimensions
    private static readonly Meter _inferenceMeter = new($"{MetricNamespace}.Inference");

    private static readonly Gauge<double> _inferenceDuration =
        _inferenceMeter.CreateGauge<double>("InferenceDuration", "ms", "Inference duration for a single item");

    public static void RecordInferenceDuration(TimeSpan duration, Dictionary<string, string>? tags)
    {
        var tagList = tags?
            .Select(p => new KeyValuePair<string, object?>(p.Key, p.Value))
            .ToArray();
        _inferenceDuration.Record(duration.TotalMilliseconds, tagList ?? []);
    }

    // public static readonly Histogram<double> InferenceThroughputHistogram =
    //     _inferenceMeter.CreateHistogram<double>("InferenceThroughput", "items/sec", description: "Inference throughput");
    //
    // public static readonly Histogram<double> InferenceParallelismHistogram =
    //     _inferenceMeter.CreateHistogram<double>("InferenceParallelism", "threads",
    //         description: "Observed number of concurrent inference threads");
    //
    // public static readonly Histogram<int> InferenceMaxParallelismHistogram =
    //     _inferenceMeter.CreateHistogram<int>("InferenceMaxParallelism", "threads",
    //         description: "Maximum number of concurrent inference threads");
    #endregion

    // #region Application
    // private static readonly Meter _applicationMeter = new($"{MetricNamespace}.Application");
    //
    // public static readonly Histogram<int> TileCountHistogram =
    //     _applicationMeter.CreateHistogram<int>("TileCount", description: "Number of tiles the image was split into");
    //
    // // Dimensions: num tiles
    // public static readonly Gauge<double> DetectionDurationGauge =
    //     _applicationMeter.CreateGauge<double>("DetectionDuration", "ms",
    //         description: "Duration of detection for a full image");
    //
    // // Dimensions: num tiles
    // public static readonly Gauge<double> DetectionThroughputGauge =
    //     _applicationMeter.CreateGauge<double>("DetectionThroughput", "images/sec",
    //         description: "Detection throughput for full images");
    //
    // // Dimensions: num words (bucketed by 100)
    // public static readonly Gauge<double> RecognitionDurationGauge =
    //     _applicationMeter.CreateGauge<double>("DetectionDuration", "ms",
    //         description: "Duration of recognition for a full image");
    //
    // // Dimensions: num words (bucketed by 100)
    // public static readonly Gauge<double> RecognitionThroughputGauge =
    //     _applicationMeter.CreateGauge<double>("DetectionThroughput", "images/sec",
    //         description: "Recognition throughput for full images");
    // #endregion
}


// public class InferenceTelemetryRecorder
// {
//     private readonly TagList _tags;
//
//     public InferenceTelemetryRecorder(Model model, ModelPrecision precision) => _tags =
//         [
//             new KeyValuePair<string, object?>("model", model.ToString()),
//             new KeyValuePair<string, object?>("precision", precision.ToString())
//         ];
//
//     public void RecordDuration(TimeSpan duration) => TelemetryRecorder.InferenceDurationHistogram.Record(duration.TotalMilliseconds, _tags);
//
//     public void RecordThroughput(double throughput) => TelemetryRecorder.InferenceThroughputHistogram.Record(throughput, _tags);
//
//     public void RecordParallelism(double parallelism) => TelemetryRecorder.InferenceParallelismHistogram.Record(parallelism, _tags);
//
//     public void RecordMaxParallelism(int maxParallelism) => TelemetryRecorder.InferenceMaxParallelismHistogram.Record(maxParallelism, _tags);
// }
