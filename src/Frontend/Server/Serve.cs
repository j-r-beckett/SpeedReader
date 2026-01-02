// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using SpeedReader.Ocr;
using SpeedReader.Ocr.InferenceEngine;
using SpeedReader.Resources.Web;
using Model = SpeedReader.Ocr.InferenceEngine.Model;

namespace SpeedReader.Frontend.Server;

public static class Serve
{
    public static async Task RunServer()
    {
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
                MaxParallelism = 1
            },
            RecognitionEngine = new CpuEngineConfig
            {
                Kernel = new OnnxInferenceKernelOptions(
                    model: Model.Svtr,
                    quantization: Quantization.Fp32,
                    numIntraOpThreads: 1),
                MaxParallelism = 1
            }
        };
        builder.Services.AddOcrPipeline(ocrPipelineOptions);

        // Configure OpenTelemetry
        builder.Services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName: "SpeedReader"))
            .WithMetrics(metrics => metrics
                .AddMeter("speedreader.inference.cpu")
                .AddAspNetCoreInstrumentation()
                .AddRuntimeInstrumentation()
                .AddProcessInstrumentation()
                .AddOtlpExporter((exporterOptions, readerOptions) =>
                {
                    exporterOptions.Endpoint = new Uri("http://otel-collector:4317");
                    readerOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 1000;
                }));

        var app = builder.Build();

        app.UseWebSockets();

#pragma warning disable IL2026, IL3050 // Suppress strict AOT warnings for minimal API route registration, it's safe
        app.MapGet("/api/health", () => "Healthy");
        app.MapPost("/api/ocr", Rest.PostOcr);
        app.Map("/api/ws/ocr", Websockets.HandleOcrWebSocket);

        // Serve embedded demo page
        var embeddedWeb = new EmbeddedWeb();
        app.MapGet("/", () => Results.Content(embeddedWeb.DemoHtml, "text/html"));
        app.MapGet("/demo", () => Results.Content(embeddedWeb.DemoHtml, "text/html"));
#pragma warning restore IL2026, IL3050

        Console.WriteLine("Starting SpeedReader server...");
        await app.RunAsync();
    }
}
