// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using TestUtils;
using Xunit.Abstractions;

namespace Core.Test;

public class WebSocketTests : IClassFixture<ServerFixture>
{
    private readonly ServerFixture _server;
    private readonly Font _font;
    private readonly FileSystemUrlPublisher<WebSocketTests> _imageSaver;

    public WebSocketTests(ServerFixture server, ITestOutputHelper outputHelper)
    {
        _server = server;
        _font = Resources.Fonts.GetFont(fontSize: 18f);
        _imageSaver = new FileSystemUrlPublisher<WebSocketTests>("/tmp", new TestLogger<WebSocketTests>(outputHelper));
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

    private async Task SendImageAsync(ClientWebSocket webSocket, byte[] imageBytes) => await webSocket.SendAsync(
            new ArraySegment<byte>(imageBytes),
            WebSocketMessageType.Binary,
            endOfMessage: true,
            CancellationToken.None);

    private async Task<string> ReceiveTextMessageAsync(ClientWebSocket webSocket)
    {
        var buffer = new byte[4096];
        using var ms = new MemoryStream();

        while (true)
        {
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                throw new InvalidOperationException("WebSocket closed unexpectedly");
            }

            ms.Write(buffer, 0, result.Count);

            if (result.EndOfMessage)
            {
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }
    }

    [Fact]
    public async Task SingleImageOverWebSocket_ReturnsOcrResult()
    {
        // Create test image
        using var testImage = CreateImageWithText("hello world");
        var imageBytes = await SaveImageToBytes(testImage);

        // Save debug image
        await _imageSaver.PublishAsync(testImage, "ws_single_image_input");

        // Connect to WebSocket endpoint
        using var webSocket = new ClientWebSocket();
        var httpUri = _server.HttpClient.BaseAddress!;
        var wsUri = new UriBuilder(httpUri)
        {
            Scheme = "ws",
            Path = "/api/ws/ocr"
        }.Uri;
        await webSocket.ConnectAsync(wsUri, CancellationToken.None);

        // Send image
        await SendImageAsync(webSocket, imageBytes);

        // Close our side to signal we're done sending
        await webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);

        // Receive result
        var resultJson = await ReceiveTextMessageAsync(webSocket);

        // Parse result
        var result = JsonSerializer.Deserialize<JsonElement>(resultJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Verify result structure
        Assert.True(result.TryGetProperty("results", out var results));
        var textResults = results.EnumerateArray();
        var allText = string.Join(" ", textResults.Select(tr => tr.GetProperty("text").GetString() ?? ""));
        Assert.Contains("hello", allText.ToLower());
        Assert.Contains("world", allText.ToLower());

        // Wait for server to close
        var buffer = new byte[1024];
        var closeResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        Assert.Equal(WebSocketMessageType.Close, closeResult.MessageType);
    }

    [Fact]
    public async Task MultipleImagesOverWebSocket_ReturnsOcrResults()
    {
        // Create test images
        using var helloImage = CreateImageWithText("hello");
        using var worldImage = CreateImageWithText("world");
        using var fooImage = CreateImageWithText("foo bar");

        var helloBytes = await SaveImageToBytes(helloImage);
        var worldBytes = await SaveImageToBytes(worldImage);
        var fooBytes = await SaveImageToBytes(fooImage);

        // Save debug images
        await _imageSaver.PublishAsync(helloImage, "ws_multi_hello_input");
        await _imageSaver.PublishAsync(worldImage, "ws_multi_world_input");
        await _imageSaver.PublishAsync(fooImage, "ws_multi_foo_input");

        // Connect to WebSocket endpoint
        using var webSocket = new ClientWebSocket();
        var httpUri = _server.HttpClient.BaseAddress!;
        var wsUri = new UriBuilder(httpUri)
        {
            Scheme = "ws",
            Path = "/api/ws/ocr"
        }.Uri;
        await webSocket.ConnectAsync(wsUri, CancellationToken.None);

        // Send all three images
        await SendImageAsync(webSocket, helloBytes);
        await SendImageAsync(webSocket, worldBytes);
        await SendImageAsync(webSocket, fooBytes);

        // Close our side to signal we're done sending
        await webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);

        // Receive three results
        var results = new List<JsonElement>();
        for (int i = 0; i < 3; i++)
        {
            var resultJson = await ReceiveTextMessageAsync(webSocket);
            var result = JsonSerializer.Deserialize<JsonElement>(resultJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            results.Add(result);
        }

        // Verify we got three results
        Assert.Equal(3, results.Count);

        // Verify first image (hello)
        Assert.True(results[0].TryGetProperty("results", out var firstResults));
        var firstText = string.Join(" ", firstResults.EnumerateArray().Select(tr => tr.GetProperty("text").GetString() ?? ""));
        Assert.Contains("hello", firstText.ToLower());

        // Verify second image (world)
        Assert.True(results[1].TryGetProperty("results", out var secondResults));
        var secondText = string.Join(" ", secondResults.EnumerateArray().Select(tr => tr.GetProperty("text").GetString() ?? ""));
        Assert.Contains("world", secondText.ToLower());

        // Verify third image (foo bar)
        Assert.True(results[2].TryGetProperty("results", out var thirdResults));
        var thirdText = string.Join(" ", thirdResults.EnumerateArray().Select(tr => tr.GetProperty("text").GetString() ?? ""));
        Assert.Contains("foo", thirdText.ToLower());
        Assert.Contains("bar", thirdText.ToLower());

        // Wait for server to close
        var buffer = new byte[1024];
        var closeResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        Assert.Equal(WebSocketMessageType.Close, closeResult.MessageType);
    }

    [Fact]
    public async Task MixedValidAndInvalidImages_ReturnsResultsAndErrors()
    {
        // Create test images
        using var validImage = CreateImageWithText("valid");
        var validBytes = await SaveImageToBytes(validImage);
        var invalidBytes = Encoding.UTF8.GetBytes("This is not an image");

        // Save debug image
        await _imageSaver.PublishAsync(validImage, "ws_mixed_valid_input");

        // Connect to WebSocket endpoint
        using var webSocket = new ClientWebSocket();
        var httpUri = _server.HttpClient.BaseAddress!;
        var wsUri = new UriBuilder(httpUri)
        {
            Scheme = "ws",
            Path = "/api/ws/ocr"
        }.Uri;
        await webSocket.ConnectAsync(wsUri, CancellationToken.None);

        // Send valid image, then invalid data, then another valid image
        await SendImageAsync(webSocket, validBytes);
        await SendImageAsync(webSocket, invalidBytes);
        await SendImageAsync(webSocket, validBytes);

        // Close our side to signal we're done sending
        await webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);

        // Receive three messages (result, error, result)
        var messages = new List<string>();
        for (int i = 0; i < 3; i++)
        {
            var message = await ReceiveTextMessageAsync(webSocket);
            messages.Add(message);
        }

        Assert.Equal(3, messages.Count);

        // First message should be a successful result
        var firstResult = JsonSerializer.Deserialize<JsonElement>(messages[0], new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        Assert.True(firstResult.TryGetProperty("results", out var firstResults));
        var firstText = string.Join(" ", firstResults.EnumerateArray().Select(tr => tr.GetProperty("text").GetString() ?? ""));
        Assert.Contains("valid", firstText.ToLower());

        // Second message should be an error
        var secondResult = JsonSerializer.Deserialize<JsonElement>(messages[1], new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        Assert.True(secondResult.TryGetProperty("error", out var error));
        Assert.Contains("Invalid image format", error.GetString());

        // Third message should be a successful result
        var thirdResult = JsonSerializer.Deserialize<JsonElement>(messages[2], new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        Assert.True(thirdResult.TryGetProperty("results", out var thirdResults));
        var thirdText = string.Join(" ", thirdResults.EnumerateArray().Select(tr => tr.GetProperty("text").GetString() ?? ""));
        Assert.Contains("valid", thirdText.ToLower());

        // Wait for server to close
        var buffer = new byte[1024];
        var closeResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        Assert.Equal(WebSocketMessageType.Close, closeResult.MessageType);
    }
}
