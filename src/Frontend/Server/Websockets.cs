// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http;
using Ocr;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

namespace Frontend.Server;

public static class Websockets
{
    public static async Task HandleOcrWebSocket(HttpContext context, OcrPipeline speedReader)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = 400;
            return;
        }

        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        await ProcessWebSocket(webSocket, speedReader);
    }

    private static async Task ProcessWebSocket(WebSocket webSocket, OcrPipeline speedReader)
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
            await foreach (var resultWrapper in speedReader.ReadMany(inputBuffer.Reader.ReadAllAsync()))
            {
                var result = resultWrapper.Value();
                try
                {
                    var jsonResult = new OcrJsonResult(
                        Filename: null,
                        Results: result.Results.Select(r => new OcrTextResult(
                            BoundingBox: r.BBox,
                            Text: r.Text,
                            Confidence: r.Confidence
                        )).ToList()
                    );

                    var json = JsonSerializer.Serialize(jsonResult, JsonContext.Default.OcrJsonResult);
                    var jsonBytes = Encoding.UTF8.GetBytes(json);
                    await webSocket.SendAsync(
                        new ArraySegment<byte>(jsonBytes),
                        WebSocketMessageType.Text,
                        endOfMessage: true,
                        CancellationToken.None);
                }
                catch (Exception ex)
                {
                    var errorResponse = new ErrorResponse($"Processing error: {ex.Message}");
                    var errorJson = JsonSerializer.Serialize(errorResponse, JsonContext.Default.ErrorResponse);
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
}
