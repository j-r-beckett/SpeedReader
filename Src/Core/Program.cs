using System.CommandLine;
using System.Diagnostics.Metrics;
using System.Text.Json;
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

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Create root command
        var rootCommand = new RootCommand("SpeedReader - Blazing fast OCR");

        // Create process subcommand
        var processCommand = new Command("process", "Process a single image file");

        var inputArgument = new Argument<FileInfo>(
            name: "input",
            description: "Input image file");

        var outputArgument = new Argument<FileInfo?>(
            name: "output",
            description: "Output image file")
        {
            Arity = ArgumentArity.ZeroOrOne
        };

        var vizOption = new Option<VizMode>(
            name: "--viz",
            description: "Visualization mode",
            getDefaultValue: () => VizMode.Basic);

        processCommand.AddArgument(inputArgument);
        processCommand.AddArgument(outputArgument);
        processCommand.AddOption(vizOption);

        processCommand.SetHandler(async (input, output, vizMode) =>
        {
            await ProcessFile(input, output, vizMode);
        }, inputArgument, outputArgument, vizOption);

        // Create serve subcommand
        var serveCommand = new Command("serve", "Run as HTTP server");

        serveCommand.SetHandler(async () =>
        {
            await RunServer();
        });

        // Add subcommands to root
        rootCommand.AddCommand(processCommand);
        rootCommand.AddCommand(serveCommand);

        return await rootCommand.InvokeAsync(args);
    }

    private static async Task ProcessFile(FileInfo input, FileInfo? output, VizMode vizMode)
    {
        try
        {
            // Validate input file exists
            if (!input.Exists)
            {
                Console.Error.WriteLine($"Error: Input file '{input.FullName}' not found.");
                Environment.Exit(1);
            }

            // Generate output filename if not specified
            if (output == null)
            {
                var inputDir = Path.GetDirectoryName(input.FullName) ?? ".";
                var inputName = Path.GetFileNameWithoutExtension(input.FullName);
                var inputExt = Path.GetExtension(input.FullName);
                var outputPath = Path.Combine(inputDir, $"{inputName}_viz{inputExt}");
                output = new FileInfo(outputPath);
            }

            // Load input image
            using var image = await Image.LoadAsync<Rgb24>(input.FullName);

            // Initialize metrics
            var meter = new Meter("SpeedReader.Ocr");


            // Create OCR pipeline
            using var modelProvider = new ModelProvider();
            var dbnetSession = modelProvider.GetSession(Model.DbNet18, ModelPrecision.INT8);
            var svtrSession = modelProvider.GetSession(Model.SVTRv2);

            var ocrBlock = OcrBlock.Create(dbnetSession, svtrSession, new OcrConfiguration(), meter);
            await using var ocrBridge = new DataflowBridge<(Image<Rgb24>, VizBuilder), (Image<Rgb24>, OcrResult, VizBuilder)>(ocrBlock);

            // Create VizBuilder and process through bridge
            var vizBuilder = VizBuilder.Create(vizMode, image);
            var resultTask = await ocrBridge.ProcessAsync((image, vizBuilder), CancellationToken.None, CancellationToken.None);
            var result = await resultTask;

            // Update page number
            var ocrResults = result.Item2 with { PageNumber = 0 };

            // Output JSON to stdout
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var json = JsonSerializer.Serialize(ocrResults, jsonOptions);
            Console.WriteLine(json);

            // Generate visualization using VizBuilder
            if (vizMode != VizMode.None)
            {
                var outputImage = result.Item3.Render();

                // Save output image
                await outputImage.SaveAsync(output.FullName);
                Console.WriteLine($"OCR visualization saved to: {output.FullName}");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.Error.WriteLine($"Stack trace: {ex}");
            Environment.Exit(1);
        }
    }

    private static async Task RunServer()
    {
        // Initialize metrics
        var meter = new Meter("SpeedReader.Ocr");

        // Create shared inference sessions
        using var modelProvider = new ModelProvider();
        var dbnetSession = modelProvider.GetSession(Model.DbNet18, ModelPrecision.INT8);
        var svtrSession = modelProvider.GetSession(Model.SVTRv2);

        // Create singleton OCR bridge
        var ocrBlock = OcrBlock.Create(dbnetSession, svtrSession, new OcrConfiguration(), meter);
        var ocrBridge = new DataflowBridge<(Image<Rgb24>, VizBuilder), (Image<Rgb24>, OcrResult, VizBuilder)>(ocrBlock);

        // Create minimal web app
        var builder = WebApplication.CreateSlimBuilder();

        // Register OCR bridge as singleton
        builder.Services.AddSingleton(ocrBridge);

        var app = builder.Build();

        app.MapGet("/api/health", () => "Healthy");

        app.MapPost("api/ocr", async (HttpContext context, DataflowBridge<(Image<Rgb24>, VizBuilder), (Image<Rgb24>, OcrResult, VizBuilder)> ocrBridge) =>
        {
            var tasks = new List<Task<(Image<Rgb24> Image, OcrResult Result, VizBuilder VizBuilder)>>();

            await foreach (var image in ParseImagesFromRequest(context.Request))
            {
                var vizBuilder = VizBuilder.Create(VizMode.None, image);
                var ocrTask = await ocrBridge.ProcessAsync((image, vizBuilder), CancellationToken.None, CancellationToken.None);
                tasks.Add(ocrTask);
            }

            if (tasks.Count == 0)
            {
                throw new BadHttpRequestException("No images found in request");
            }

            // Await all tasks - fail fast if any fail
            var results = await Task.WhenAll(tasks);

            // Process results and create response
            var ocrResults = new List<OcrResult>();
            for (int i = 0; i < results.Length; i++)
            {
                var (image, result, _) = results[i];
                ocrResults.Add(result with { PageNumber = i });

                // Dispose image after processing
                image.Dispose();
            }

            // Return JSON response
            context.Response.ContentType = "application/json";
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            // Always return array for consistent API
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
            var boundary = request.GetMultipartBoundary();
            var reader = new MultipartReader(boundary, request.Body);

            await foreach (var section in request.ExtractSectionsAsync())
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
}

public static class HttpRequestExtensions
{
    public static async IAsyncEnumerable<MultipartSection> ExtractSectionsAsync(this  HttpRequest request)
    {
        var boundary = request.GetMultipartBoundary();
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

    public static string GetMultipartBoundary(this HttpRequest request)
    {
        var contentType = request.ContentType ?? throw new BadHttpRequestException("No Content-Type header");
        var boundaryIndex = contentType.IndexOf("boundary=");
        if (boundaryIndex == -1) throw new BadHttpRequestException("No boundary found in Content-Type header");

        var boundary = contentType.Substring(boundaryIndex + 9).Split(';')[0].Trim(' ', '"');
        return boundary;
    }
}
