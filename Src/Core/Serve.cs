// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics.Metrics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Experimental;
using Experimental.Inference;
using Experimental.Telemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Ocr;
using Ocr.Blocks;
using Ocr.Visualization;
using Resources;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

namespace Core;

public static class Serve
{
    public static async Task RunServer()
    {
        // Create performance metrics collection
        var metricsChannel = Channel.CreateUnbounded<MetricPoint>();
        var processMetricsCollector = new ProcessMetricsCollector(metricsChannel.Writer);
        var containerMetricsCollector = ContainerMetricsCollector.IsRunningInContainer()
            ? new ContainerMetricsCollector(metricsChannel.Writer)
            : null;
        var metricsWriter = new TimescaleDbWriter(metricsChannel.Reader);

        // Create shared inference sessions
        using var modelProvider = new ModelProvider();
        var dbnetRunner = new CpuModelRunner(Model.DbNet18, ModelPrecision.INT8, modelProvider.GetSession(Model.DbNet18, ModelPrecision.INT8));
        // var dbnetRunner = new CpuModelRunner(modelProvider.GetSession(Model.DbNet18, ModelPrecision.INT8), 4);
        var svtrRunner = new CpuModelRunner(modelProvider.GetSession(Model.SVTRv2), 4);
        var speedReader = new SpeedReader(dbnetRunner, svtrRunner, 4, 1);

        // Create minimal web app
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddSingleton(speedReader);

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

        app.MapGet("/api/health", () => "Healthy");

        app.MapPost("/api/ocr", async (HttpContext context, SpeedReader speedReader) =>
        {
            var images = ParseImagesFromRequest(context.Request);
            var results = speedReader.ReadMany(images);

            var ocrResults = new List<object>();
            await foreach (var result in results)
            {
                try
                {
                    var jsonResult = new
                    {
                        Results = result.Results.Select(r => new
                        {
                            BoundingBox = r.BBox,
                            r.Text,
                            r.Confidence
                        }).ToList()
                    };
                    ocrResults.Add(jsonResult);
                }
                finally
                {
                    result.Image.Dispose();
                }
            }

            if (ocrResults.Count == 0)
            {
                throw new BadHttpRequestException("No images found in request");
            }

            // Return JSON response
            context.Response.ContentType = "application/json";
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(ocrResults, jsonOptions);
            await context.Response.WriteAsync(json);
        });

        app.Map("/api/ws/ocr", async (HttpContext context, SpeedReader speedReader) =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                return;
            }

            using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            await HandleWebSocketOcr(webSocket, speedReader);
        });

        // // Map OpenTelemetry Prometheus metrics endpoint
        // app.MapPrometheusScrapingEndpoint();

        Console.WriteLine("Starting SpeedReader server...");
        await app.RunAsync();
    }

    private static async Task HandleWebSocketOcr(WebSocket webSocket, SpeedReader speedReader)
    {
        var inputBuffer = Channel.CreateBounded<Image<Rgb24>>(1);
        var config = Configuration.Default.Clone();
        config.PreferContiguousImageBuffers = true;
        var decoderOptions = new DecoderOptions { Configuration = config };

        var receiveTask = Task.Run(async () =>
        {
            try
            {
                while (webSocket.State == WebSocketState.Open)
                {
                    var messageBytes = await ReceiveCompleteMessageAsync(webSocket);
                    if (messageBytes == null)
                        break;

                    using var memoryStream = new MemoryStream(messageBytes);
                    var image = await Image.LoadAsync<Rgb24>(decoderOptions, memoryStream);
                    await inputBuffer.Writer.WriteAsync(image);
                }
            }
            finally
            {
                inputBuffer.Writer.Complete();
            }
        });

        var sendTask = Task.Run(async () =>
        {
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            await foreach (var result in speedReader.ReadMany(inputBuffer.Reader.ReadAllAsync()))
            {
                try
                {
                    var jsonResult = new
                    {
                        Results = result.Results.Select(r => new
                        {
                            BoundingBox = r.BBox,
                            r.Text,
                            r.Confidence
                        }).ToList()
                    };

                    var json = JsonSerializer.Serialize(jsonResult, jsonOptions);
                    var jsonBytes = Encoding.UTF8.GetBytes(json);
                    await webSocket.SendAsync(
                        new ArraySegment<byte>(jsonBytes),
                        WebSocketMessageType.Text,
                        endOfMessage: true,
                        CancellationToken.None);
                }
                catch (Exception ex)
                {
                    var errorJson = JsonSerializer.Serialize(new { Error = $"Processing error: {ex.Message}" }, jsonOptions);
                    var errorBytes = Encoding.UTF8.GetBytes(errorJson);
                    await webSocket.SendAsync(
                        new ArraySegment<byte>(errorBytes),
                        WebSocketMessageType.Text,
                        endOfMessage: true,
                        CancellationToken.None);
                }
                finally
                {
                    result.Image.Dispose();
                }
            }

            await webSocket.CloseAsync(
                WebSocketCloseStatus.NormalClosure,
                "Processing complete",
                CancellationToken.None);
        });

        await Task.WhenAll(receiveTask, sendTask);
    }

    private static async Task<byte[]?> ReceiveCompleteMessageAsync(WebSocket webSocket)
    {
        var buffer = new byte[4096];
        using var ms = new MemoryStream();

        while (true)
        {
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            ms.Write(buffer, 0, result.Count);

            if (result.EndOfMessage)
            {
                return ms.ToArray();
            }
        }
    }

    private static async IAsyncEnumerable<Image<Rgb24>> ParseImagesFromRequest(HttpRequest request)
    {
        var contentType = request.ContentType ?? "";

        // ImageSharp decoder options; used in multipart and single image flows
        var config = Configuration.Default.Clone();
        config.PreferContiguousImageBuffers = true;
        var decoderOptions = new DecoderOptions { Configuration = config };

        if (contentType.StartsWith("multipart/"))
        {
            await foreach (var section in ExtractSectionsAsync(request))
            {
                var contentDisposition = section.GetContentDispositionHeader();

                if (contentDisposition?.FileName != null)
                {
                    Image<Rgb24> image;
                    try
                    {
                        image = await Image.LoadAsync<Rgb24>(decoderOptions, section.Body);
                    }
                    catch (UnknownImageFormatException ex)
                    {
                        throw new BadHttpRequestException(
                            $"Invalid image format in file '{contentDisposition.FileName}': {ex.Message}");
                    }

                    yield return image;
                }
            }
        }
        else if (contentType.StartsWith("application/") || contentType.StartsWith("image/") || string.IsNullOrEmpty(contentType))
        {
            using var memoryStream = new MemoryStream();
            await request.Body.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            Image<Rgb24> image;
            try
            {
                image = await Image.LoadAsync<Rgb24>(decoderOptions, memoryStream);
            }
            catch (UnknownImageFormatException ex)
            {
                throw new BadHttpRequestException($"Invalid image format: {ex.Message}");
            }
            yield return image;
        }
        else
        {
            throw new BadHttpRequestException($"Unsupported content type: {contentType}");
        }
    }

    private static async IAsyncEnumerable<MultipartSection> ExtractSectionsAsync(HttpRequest request)
    {
        var contentType = request.ContentType ?? throw new BadHttpRequestException("No Content-Type header");
        var boundaryIndex = contentType.IndexOf("boundary=");
        if (boundaryIndex == -1)
        {
            throw new BadHttpRequestException("No boundary found in Content-Type header");
        }
        var boundary = contentType.Substring(boundaryIndex + 9).Split(';')[0].Trim(' ', '"');

        var reader = new MultipartReader(boundary, request.Body);

        // We can't wrap the while loop in a try/catch because you can't yield return from inside a try/catch, so we
        // need this awkward while(true) construct
        while (true)
        {
            MultipartSection? section;
            try
            {
                section = await reader.ReadNextSectionAsync();
            }
            catch (IOException)
            {
                section = null;
            }

            if (section is null)
            {
                break;
            }

            yield return section;
        }
    }
}
