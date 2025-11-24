// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Ocr;
using Ocr.InferenceEngine;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using Model = Ocr.InferenceEngine.Model;

namespace Frontend.Server;

public static class Serve
{
    public static async Task RunServer()
    {
        // Create performance metrics collection
        // var metricsChannel = Channel.CreateUnbounded<MetricPoint>();
        // var processMetricsCollector = new ProcessMetricsCollector();
        // var containerMetricsCollector = ContainerMetricsCollector.IsRunningInContainer()
        //     ? new ContainerMetricsCollector()
        //     : null;
        // var metricsWriter = new MetricRecorder(metricsChannel.Reader);

        // Create minimal web app
        var builder = WebApplication.CreateSlimBuilder();

        // Register OcrPipeline and dependencies using DI
        var ocrPipelineOptions = new OcrPipelineOptions
        {
            DetectionOptions = new DetectionOptions(),
            RecognitionOptions = new RecognitionOptions(),
            DetectionEngine = new CpuEngineConfig
            {
                Kernel = new OnnxInferenceKernelOptions(
                    model: Model.DbNet,
                    quantization: Quantization.Int8,
                    numIntraOpThreads: 1),
                Parallelism = 1
            },
            RecognitionEngine = new CpuEngineConfig
            {
                Kernel = new OnnxInferenceKernelOptions(
                    model: Model.Svtr,
                    quantization: Quantization.Fp32,
                    numIntraOpThreads: 1),
                Parallelism = 1
            }
        };
        builder.Services.AddOcrPipeline(ocrPipelineOptions);

        // // Configure OpenTelemetry with Prometheus exporter
        // builder.Services.AddOpenTelemetry()
        //     .WithMetrics(metrics => metrics
        //         .AddMeter("SpeedReader.Inference")
        //         .AddMeter("SpeedReader.Application")
        //         .AddView("InferenceDuration", new ExplicitBucketHistogramConfiguration
        //         {
        //             Boundaries = Enumerable.Range(0, 40).Select(i => i * 25.0).ToArray()
        //         })
        //         .AddPrometheusExporter());

        var app = builder.Build();

        app.UseWebSockets();

#pragma warning disable IL2026, IL3050 // Suppress strict AOT warnings for minimal API route registration, it's safe
        app.MapGet("/api/health", () => "Healthy");
        app.MapPost("/api/ocr", Rest.PostOcr);
        app.Map("/api/ws/ocr", Websockets.HandleOcrWebSocket);
#pragma warning restore IL2026, IL3050

        // // Map OpenTelemetry Prometheus metrics endpoint
        // app.MapPrometheusScrapingEndpoint();

        Console.WriteLine("Starting SpeedReader server...");
        await app.RunAsync();
    }
}
