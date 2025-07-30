using System.Diagnostics;
using System.Text.Json;
using CliWrap;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using TestUtils;
using Xunit.Abstractions;

namespace Core.Test;

public class StreamingUploadE2ETests : IDisposable
{
    private readonly Font _font;
    private readonly FileSystemUrlPublisher<StreamingUploadE2ETests> _imageSaver;
    private readonly HttpClient _httpClient;
    private readonly CancellationTokenSource _serverCancellation;
    private readonly Task _serverTask;
    private readonly int _serverPort;

    public StreamingUploadE2ETests(ITestOutputHelper outputHelper)
    {
        _font = Resources.Fonts.GetFont(fontSize: 18f);
        _imageSaver = new FileSystemUrlPublisher<StreamingUploadE2ETests>("/tmp", new TestLogger<StreamingUploadE2ETests>(outputHelper));

        // Find available port
        _serverPort = GetAvailablePort();

        // Start server process using CliWrap
        _serverCancellation = new CancellationTokenSource();
        _serverTask = StartServerAsync(_serverCancellation.Token);

        // Wait for server to start
        WaitForServerReady();

        // Create HTTP client
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri($"http://localhost:{_serverPort}")
        };
    }

    private int GetAvailablePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private Task StartServerAsync(CancellationToken cancellationToken)
    {
        var command = Cli.Wrap("dotnet")
            .WithArguments($"run --project {GetCoreProjectPath()} -- serve")
            .WithEnvironmentVariables(env => env.Set("ASPNETCORE_URLS", $"http://localhost:{_serverPort}"))
            .WithValidation(CommandResultValidation.None); // Don't throw on non-zero exit codes

        return command.ExecuteAsync(cancellationToken);
    }

    private string GetCoreProjectPath()
    {
        // Navigate up from test directory to find Core project
        var currentDir = Directory.GetCurrentDirectory();
        while (currentDir != null && !File.Exists(Path.Combine(currentDir, "SpeedReader.sln")))
        {
            currentDir = Directory.GetParent(currentDir)?.FullName;
        }

        if (currentDir == null)
            throw new Exception("Could not find SpeedReader.sln");

        return Path.Combine(currentDir, "Src", "Core");
    }

    private void WaitForServerReady()
    {
        var timeout = TimeSpan.FromSeconds(30);
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeout)
        {
            try
            {
                using var client = new HttpClient();
                var response = client.GetAsync($"http://localhost:{_serverPort}/health").Result;
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch
            {
                // Server not ready yet
            }

            Thread.Sleep(500);
        }

        throw new Exception($"Server failed to start within {timeout}");
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
        await image.SaveAsJpegAsync(memoryStream);
        return memoryStream.ToArray();
    }

    [Fact]
    public async Task StreamingUpload_TwoImages_ReturnsCorrectOcrResults()
    {
        // Create test images
        using var helloImage = CreateImageWithText("hello");
        using var worldImage = CreateImageWithText("world");

        // Save debug images for inspection
        await _imageSaver.PublishAsync(helloImage, "hello_input");
        await _imageSaver.PublishAsync(worldImage, "world_input");

        // Convert to byte arrays
        var helloBytes = await SaveImageToBytes(helloImage);
        var worldBytes = await SaveImageToBytes(worldImage);

        // Create multipart form content
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(helloBytes), "images", "hello.jpg");
        content.Add(new ByteArrayContent(worldBytes), "images", "world.jpg");

        // Send request to streaming endpoint
        var response = await _httpClient.PostAsync("/api/ocr/stream", content);

        // Verify response
        response.EnsureSuccessStatusCode();
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        // Parse response
        var responseBody = await response.Content.ReadAsStringAsync();
        var results = JsonSerializer.Deserialize<JsonElement[]>(responseBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Verify we got results for both images
        Assert.NotNull(results);
        Assert.Equal(2, results.Length);

        // Verify first image (hello)
        var firstResult = results[0];
        Assert.Equal(0, firstResult.GetProperty("pageNumber").GetInt32());

        // Check for text in lines
        var firstLines = firstResult.GetProperty("lines").EnumerateArray();
        var firstAllText = string.Join(" ", firstLines.Select(line => line.GetProperty("text").GetString() ?? ""));
        Assert.Contains("hello", firstAllText.ToLower());

        // Verify second image (world)
        var secondResult = results[1];
        Assert.Equal(1, secondResult.GetProperty("pageNumber").GetInt32());

        // Check for text in lines
        var secondLines = secondResult.GetProperty("lines").EnumerateArray();
        var secondAllText = string.Join(" ", secondLines.Select(line => line.GetProperty("text").GetString() ?? ""));
        Assert.Contains("world", secondAllText.ToLower());
    }

    public void Dispose()
    {
        _httpClient.Dispose();

        // Cancel the server process
        _serverCancellation.Cancel();

        try
        {
            // Wait for server to shut down gracefully
            _serverTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException)
        {
            // Expected when cancellation occurs
        }
        finally
        {
            _serverCancellation.Dispose();
        }
    }
}
