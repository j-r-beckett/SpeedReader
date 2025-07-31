using System.Text.Json;
using System.Threading.Channels;
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
    private readonly CancellationTokenSource _forcefulShutdownCts = new();

    public StreamingUploadE2ETests(ITestOutputHelper outputHelper)
    {
        _font = Resources.Fonts.GetFont(fontSize: 18f);
        _imageSaver = new FileSystemUrlPublisher<StreamingUploadE2ETests>("/tmp", new TestLogger<StreamingUploadE2ETests>(outputHelper));

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
                var response = await client.GetAsync($"http://localhost:{port}/health", cts.Token);
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
        // Find available port
        var serverPort = GetAvailablePort();

        // Start server using CliWrap with the built DLL
        var serverDll = Path.Combine(AppContext.BaseDirectory, "speedread.dll");
        
        // Setup graceful shutdown token
        using var gracefulCts = new CancellationTokenSource();

        var serverCommand = Cli.Wrap("dotnet")
            .WithArguments($"{serverDll} serve")
            .WithEnvironmentVariables(env => env.Set("ASPNETCORE_URLS", $"http://localhost:{serverPort}"))
            .WithValidation(CommandResultValidation.None); // Don't throw on non-zero exit codes

        // Start server task
        var serverTask = serverCommand.ExecuteAsync(_forcefulShutdownCts.Token, gracefulCts.Token);

        // Wait for server to be ready
        await WaitForServerReady(serverPort, CancellationToken.None);

        // Create HTTP client
        using var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri($"http://localhost:{serverPort}");

        try
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
            var response = await httpClient.PostAsync("/api/ocr/stream", content);

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
        finally
        {
            // Initiate graceful shutdown
            gracefulCts.Cancel();
            
            // Set up forceful shutdown as fallback after 5 seconds
            _forcefulShutdownCts.CancelAfter(TimeSpan.FromSeconds(5));

            try
            {
                var result = await serverTask;
                
                // If we get here, the process exited normally - verify exit code 0
                Assert.Equal(0, result.ExitCode);
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken == gracefulCts.Token)
            {
                // This is expected for graceful shutdown - CliWrap throws OperationCanceledException
                // even when the process handles SIGINT gracefully. This is success!
                // (The process should have exited with code 0, but we can't check it due to CliWrap's design)
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken == _forcefulShutdownCts.Token)
            {
                throw new InvalidOperationException("Server graceful shutdown timed out after 5 seconds, was forcefully terminated");
            }
            catch (OperationCanceledException)
            {
                throw new InvalidOperationException("Server shutdown was cancelled by unknown token");
            }
        }
    }

    public void Dispose()
    {
        // Ensure forceful shutdown on disposal (handles test failures)
        _forcefulShutdownCts.Cancel();
        _forcefulShutdownCts.Dispose();
    }

}
