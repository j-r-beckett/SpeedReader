// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics.Metrics;
using System.Text.Json;
using Experimental;
using Experimental.Inference;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Ocr;
using Ocr.Blocks;
using Ocr.Visualization;
using Prometheus;
using Resources;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

namespace Core;

public static class Serve
{
    public static async Task RunServer()
    {
        // Create shared inference sessions
        using var modelProvider = new ModelProvider();
        var dbnetRunner = new CpuModelRunner(modelProvider.GetSession(Model.DbNet18, ModelPrecision.INT8), 4);
        var svtrRunner = new CpuModelRunner(modelProvider.GetSession(Model.SVTRv2), 4);
        var speedReader = new SpeedReader(dbnetRunner, svtrRunner, 4, 1);

        // Create minimal web app
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddSingleton(speedReader);

        var app = builder.Build();

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

        // Add Prometheus metrics endpoint (automatic)
        app.UseMetricServer();

        Console.WriteLine("Starting SpeedReader server...");
        await app.RunAsync();
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
