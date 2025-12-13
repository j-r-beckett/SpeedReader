// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Text;
using System.Text.Json;
using CliWrap;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using TestUtils;
using Xunit.Abstractions;

namespace Frontend.Test;

public class ApiE2ETests : IClassFixture<ServerFixture>
{
    private readonly ServerFixture _server;
    private readonly HttpClient _httpClient;
    private readonly Font _font;
    private readonly FileSystemUrlPublisher<ApiE2ETests> _imageSaver;

    public ApiE2ETests(ServerFixture server, ITestOutputHelper outputHelper)
    {
        _server = server;
        _httpClient = _server.HttpClient;
        _font = Resources.Font.EmbeddedFont.Default.Get(fontSize: 18f);
        _imageSaver = new FileSystemUrlPublisher<ApiE2ETests>("/tmp", new TestLogger<ApiE2ETests>(outputHelper));
    }

    private Image<Rgb24> CreateImageWithText(string text, int width = 400, int height = 200)
    {
        var image = new Image<Rgb24>(width, height, Color.White);

        image.Mutate(ctx =>
        {
            var textOptions = new RichTextOptions(_font)
            {
                Origin = new PointF(50, 50),
                WrappingLength = width - 100
            };
            ctx.DrawText(textOptions, text, Color.Black);
        });

        return image;
    }

    private async Task<byte[]> SaveImageToBytes(Image image)
    {
        using var memoryStream = new MemoryStream();
        await image.SaveAsPngAsync(memoryStream);
        return memoryStream.ToArray();
    }

    private Image<Rgb24> CreateLargeImage(int width = 2000, int height = 2000)
    {
        var image = new Image<Rgb24>(width, height);

        // Fill with random noise to prevent compression
        var random = new Random(42); // Fixed seed for reproducibility
        var pixels = new Rgb24[width * height];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Rgb24((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256));
        }

        // Load the random pixel data
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                var sourceRow = pixels.AsSpan(y * width, width);
                sourceRow.CopyTo(row);
            }
        });

        // Add multiple clear text regions for OCR to detect on top of noise
        image.Mutate(ctx =>
        {
            // Create white background patches for better text contrast
            ctx.Fill(Color.White, new RectangleF(50, 50, width - 100, 100));
            ctx.Fill(Color.White, new RectangleF(50, 200, width - 100, 100));
            ctx.Fill(Color.White, new RectangleF(50, 350, width - 100, 100));

            var textOptions = new RichTextOptions(_font)
            {
                Origin = new PointF(100, 100),
                WrappingLength = width - 200
            };
            ctx.DrawText(textOptions, "Large test image for contiguous memory verification", Color.Black);

            textOptions.Origin = new PointF(100, 250);
            ctx.DrawText(textOptions, "This image contains multiple text regions to ensure OCR processing", Color.Black);

            textOptions.Origin = new PointF(100, 400);
            ctx.DrawText(textOptions, "Random noise background with clear readable text sections", Color.Black);
        });

        return image;
    }

    [Fact]
    public async Task SingleImageUpload_ReturnsCorrectOcrResult()
    {
        // Create test image
        using var testImage = CreateImageWithText("hello world");
        var imageBytes = await SaveImageToBytes(testImage);

        // Save debug image
        await _imageSaver.PublishAsync(testImage, "single_image_input");

        // Send as application/octet-stream
        using var content = new ByteArrayContent(imageBytes);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        var response = await _httpClient.PostAsync("/api/ocr", content);

        // Verify response
        response.EnsureSuccessStatusCode();
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        // Parse response
        var responseBody = await response.Content.ReadAsStringAsync();
        var results = JsonSerializer.Deserialize<JsonElement[]>(responseBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Verify single result in array
        Assert.NotNull(results);
        Assert.Single(results);

        var result = results[0];

        // Check for text content
        var textResults = result.GetProperty("results").EnumerateArray();
        var allText = string.Join(" ", textResults.Select(tr => tr.GetProperty("text").GetString() ?? ""));
        Assert.Contains("hello", allText.ToLower());
        Assert.Contains("world", allText.ToLower());
    }

    [Fact]
    public async Task MultipleImageUpload_ReturnsCorrectOcrResults()
    {
        // Create test images
        using var helloImage = CreateImageWithText("hello");
        using var worldImage = CreateImageWithText("world");
        using var fooImage = CreateImageWithText("foo bar");

        // Save debug images
        await _imageSaver.PublishAsync(helloImage, "multi_hello_input");
        await _imageSaver.PublishAsync(worldImage, "multi_world_input");
        await _imageSaver.PublishAsync(fooImage, "multi_foo_input");

        // Convert to byte arrays
        var helloBytes = await SaveImageToBytes(helloImage);
        var worldBytes = await SaveImageToBytes(worldImage);
        var fooBytes = await SaveImageToBytes(fooImage);

        // Create multipart form content
        using var content = new MultipartFormDataContent
        {
            { new ByteArrayContent(helloBytes), "images", "hello.png" },
            { new ByteArrayContent(worldBytes), "images", "world.png" },
            { new ByteArrayContent(fooBytes), "images", "foo.png" }
        };

        var response = await _httpClient.PostAsync("/api/ocr", content);

        // Verify response
        response.EnsureSuccessStatusCode();
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        // Parse response
        var responseBody = await response.Content.ReadAsStringAsync();
        var results = JsonSerializer.Deserialize<JsonElement[]>(responseBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Verify we got results for all three images
        Assert.NotNull(results);
        Assert.Equal(3, results.Length);

        // Verify first image (hello)
        var firstResult = results[0];
        var firstTextResults = firstResult.GetProperty("results").EnumerateArray();
        var firstAllText = string.Join(" ", firstTextResults.Select(tr => tr.GetProperty("text").GetString() ?? ""));
        Assert.Contains("hello", firstAllText.ToLower());

        // Verify second image (world)
        var secondResult = results[1];
        var secondTextResults = secondResult.GetProperty("results").EnumerateArray();
        var secondAllText = string.Join(" ", secondTextResults.Select(tr => tr.GetProperty("text").GetString() ?? ""));
        Assert.Contains("world", secondAllText.ToLower());

        // Verify third image (foo bar)
        var thirdResult = results[2];
        var thirdTextResults = thirdResult.GetProperty("results").EnumerateArray();
        var thirdAllText = string.Join(" ", thirdTextResults.Select(tr => tr.GetProperty("text").GetString() ?? ""));
        Assert.Contains("foo", thirdAllText.ToLower());
        Assert.Contains("bar", thirdAllText.ToLower());
    }

    [Fact]
    public async Task InvalidImageFormat_Returns400()
    {
        // Create invalid image data (text content)
        var invalidData = System.Text.Encoding.UTF8.GetBytes("This is not an image file");

        using var content = new ByteArrayContent(invalidData);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        var response = await _httpClient.PostAsync("/api/ocr", content);

        // Should return 400 Bad Request
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task InvalidImageInMultipart_Returns400()
    {
        // Create valid image and invalid data
        using var validImage = CreateImageWithText("valid");
        var validBytes = await SaveImageToBytes(validImage);
        var invalidBytes = System.Text.Encoding.UTF8.GetBytes("invalid image data");

        using var content = new MultipartFormDataContent
        {
            { new ByteArrayContent(validBytes), "images", "valid.png" },
            { new ByteArrayContent(invalidBytes), "images", "invalid.png" }
        };

        var response = await _httpClient.PostAsync("/api/ocr", content);

        // Should return 400 Bad Request (fail fast)
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EmptyMultipartRequest_Returns400()
    {
        // Create empty multipart content
        using var content = new MultipartFormDataContent();

        var response = await _httpClient.PostAsync("/api/ocr", content);

        // Should return 400 Bad Request for empty multipart form
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnsupportedContentType_Returns400()
    {
        using var content = new StringContent("some data", System.Text.Encoding.UTF8, "text/plain");

        var response = await _httpClient.PostAsync("/api/ocr", content);

        // Should return 400 Bad Request
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task HandlesLargeUpload()
    {
        using var largeImage = CreateLargeImage();
        var imageBytes = await SaveImageToBytes(largeImage);

        // Verify image is actually large
        Assert.True(imageBytes.Length > 5_000_000, $"Image should be > 5MB, was {imageBytes.Length} bytes");

        // Save debug image (smaller version for inspection)
        using var thumbnailImage = CreateImageWithText("Large image thumbnail", 400, 200);
        await _imageSaver.PublishAsync(thumbnailImage, "large_image_thumbnail");

        using var content = new ByteArrayContent(imageBytes);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        var response = await _httpClient.PostAsync("/api/ocr", content);

        // This will fail at runtime if contiguous memory is not configured properly
        response.EnsureSuccessStatusCode();
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        // Parse response
        var responseBody = await response.Content.ReadAsStringAsync();
        var results = JsonSerializer.Deserialize<JsonElement[]>(responseBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Verify we got a result
        Assert.NotNull(results);
        Assert.Single(results);
    }
}

public class ServerFixture : IDisposable
{
    private readonly CancellationTokenSource _forcefulShutdownCts = new();
    private readonly CancellationTokenSource _gracefulCts = new();
    private readonly Task _serverTask;
    private readonly int _serverPort;
    private readonly StringBuilder _stdOutBuffer = new();
    private readonly StringBuilder _stdErrBuffer = new();
    private readonly string _logFilePath;

    public HttpClient HttpClient
    {
        get;
    }

    public ServerFixture()
    {
        // Find available port
        _serverPort = GetAvailablePort();

        // Set up log file path
        _logFilePath = Path.Combine("/tmp", $"speedreader-server-logs-{Guid.NewGuid():N}.txt");

        // Start server using CliWrap with the built DLL
        var serverDll = Path.Combine(AppContext.BaseDirectory, "speedreader.dll");

        var serverCommand = CliWrap.Cli.Wrap("dotnet")
            .WithArguments($"{serverDll} --serve")
            .WithEnvironmentVariables(env => env.Set("ASPNETCORE_URLS", $"http://localhost:{_serverPort}"))
            .WithStandardOutputPipe(PipeTarget.ToStringBuilder(_stdOutBuffer))
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(_stdErrBuffer))
            .WithValidation(CommandResultValidation.None);

        // Start server task
        _serverTask = serverCommand.ExecuteAsync(_forcefulShutdownCts.Token, _gracefulCts.Token);

        // Wait for server to be ready
        WaitForServerReady(_serverPort, CancellationToken.None).GetAwaiter().GetResult();

        // Create HTTP client
        HttpClient = new HttpClient
        {
            BaseAddress = new Uri($"http://localhost:{_serverPort}")
        };
    }

    private static int GetAvailablePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task WaitForServerReady(int port, CancellationToken cancellationToken)
    {
        var timeout = TimeSpan.FromSeconds(30);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                using var client = new HttpClient();
                var response = await client.GetAsync($"http://localhost:{port}/api/health", cts.Token);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch
            {
                // Server not ready yet
            }

            await Task.Delay(100, cts.Token);
        }

        throw new TimeoutException($"Server failed to start within {timeout}");
    }

    public void Dispose()
    {
        HttpClient?.Dispose();

        // Initiate graceful shutdown
        _gracefulCts.Cancel();

        // Set up forceful shutdown as fallback after 5 seconds
        _forcefulShutdownCts.CancelAfter(TimeSpan.FromSeconds(5));

        try
        {
            _serverTask.GetAwaiter().GetResult();
            // Process exited normally
        }
        catch (OperationCanceledException ex) when (ex.CancellationToken == _gracefulCts.Token)
        {
            // Expected for graceful shutdown
        }
        catch (OperationCanceledException ex) when (ex.CancellationToken == _forcefulShutdownCts.Token)
        {
            // Forceful shutdown timeout
        }
        catch (OperationCanceledException)
        {
            // Other cancellation
        }

        // Write server logs to file
        try
        {
            var logContent = $"=== SpeedReader Server Logs (Port: {_serverPort}) ===\n\n";
            logContent += "=== STDOUT ===\n";
            logContent += _stdOutBuffer.ToString();
            logContent += "\n\n=== STDERR ===\n";
            logContent += _stdErrBuffer.ToString();

            File.WriteAllText(_logFilePath, logContent);
            Console.WriteLine($"Server logs written to: file://wsl${_logFilePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to write server logs: {ex.Message}");
        }

        _forcefulShutdownCts.Dispose();
        _gracefulCts.Dispose();
    }
}
