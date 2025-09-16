// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CliWrap.Buffered;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Video.Test;

public class FFProbeTests
{
    [Fact]
    public async Task Returns_CorrectWidthAndHeight()
    {
        var (expectedWidth, expectedHeight) = (100, 75);

        var video = await CreateVideo(expectedWidth, expectedHeight);

        var (actualWidth, actualHeight) = await new FFProbe().GetVideoDimensions(video, default);

        Assert.Equal(expectedWidth, actualWidth);
        Assert.Equal(expectedHeight, actualHeight);
    }

    [Fact]
    public async Task ThrowsException_OnRandomData()
    {
        var random = new Random(0);
        var data = new byte[500];
        random.NextBytes(data);
        var video = new MemoryStream(data);

        var act = () => new FFProbe().GetVideoDimensions(video, default);

        await Assert.ThrowsAsync<FFPRobeException>(act);
    }

    [Theory]
    [InlineData(" 1920,1080 ", 1920, 1080)]
    [InlineData("1920,1080", 1920, 1080)]
    [InlineData("\t640,480\n", 640, 480)]
    [InlineData("0001,480", 1, 480)]
    [InlineData("640,0002", 640, 2)]
    public async Task ParsesValidOutput_WithWhitespace(string output, int expectedWidth, int expectedHeight)
    {
        var video = new MemoryStream();
        var result = new BufferedCommandResult(0, DateTimeOffset.Now, DateTimeOffset.Now, output, "");

        var (actualWidth, actualHeight) = await new MockFFProbe(() => result).GetVideoDimensions(video, default);

        Assert.Equal(expectedWidth, actualWidth);
        Assert.Equal(expectedHeight, actualHeight);
    }

    [Theory]
    [InlineData("1920x1080")]
    [InlineData("not-numbers")]
    [InlineData("1920,")]
    [InlineData(",1080")]
    [InlineData("-1,1080")]
    [InlineData("1920,-1")]
    [InlineData("0,1080")]
    [InlineData("1920,0")]
    [InlineData("")]
    [InlineData("1920,1080\n1280,720")]
    public async Task ThrowsException_OnInvalidOutput(string invalidOutput)
    {
        var video = new MemoryStream();
        var result = new BufferedCommandResult(0, DateTimeOffset.Now, DateTimeOffset.Now, invalidOutput, "");

        var act = () => new MockFFProbe(() => result).GetVideoDimensions(video, default);

        var exception = await Assert.ThrowsAsync<FFPRobeException>(act);
        Assert.Equal($"Unable to parse output {invalidOutput}", exception.Message);
    }

    [Fact]
    public async Task ThrowsOperationCanceledException_OnCancellation()
    {
        var video = new MemoryStream();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => new FFProbe().GetVideoDimensions(video, cts.Token);

        await Assert.ThrowsAsync<OperationCanceledException>(act);
    }

    [Fact]
    public async Task ThrowsException_OnEmptyStream()
    {
        var video = new MemoryStream();

        var act = () => new FFProbe().GetVideoDimensions(video, default);

        await Assert.ThrowsAsync<FFPRobeException>(act);
    }

    [Fact]
    public async Task DoesNotResetStreamPosition()
    {
        var video = await CreateVideo(100, 75);

        await new FFProbe().GetVideoDimensions(video, default);

        Assert.NotEqual(0, video.Position);
    }

    private async Task<Stream> CreateVideo(int width, int height)
    {
        const int numFrames = 12;
        const int frameRate = 3;
        var frames = new List<Image<Rgb24>>();

        for (var i = 0; i < numFrames; i++)
        {
            var frame = new Image<Rgb24>(width, height);

            frame.ProcessPixelRows(accessor =>
            {
                for (var y = 0; y < accessor.Height; y++)
                {
                    var pixelRow = accessor.GetRowSpan(y);
                    for (var x = 0; x < pixelRow.Length; x++)
                    {
                        pixelRow[x] = Color.Red;
                    }
                }
            });

            frames.Add(frame);
        }

        return await FrameWriter.ToCompressedVideo(width, height, frameRate, frames.ToAsyncEnumerable(), default);
    }

    public class MockFFProbe : FFProbe
    {
        private readonly Func<int, BufferedCommandResult>? _func;
        private readonly Func<Task<BufferedCommandResult>>? _asyncFunc;
        private readonly Func<CancellationToken, Task<BufferedCommandResult>>? _asyncFuncWithToken;
        private int _counter = -1;

        public MockFFProbe(Func<int, BufferedCommandResult> func) => _func = func;

        public MockFFProbe(Func<BufferedCommandResult> func) => _func = _ => func();

        public MockFFProbe(Func<Task<BufferedCommandResult>> asyncFunc) => _asyncFunc = asyncFunc;

        public MockFFProbe(Func<CancellationToken, Task<BufferedCommandResult>> asyncFuncWithToken) => _asyncFuncWithToken = asyncFuncWithToken;

        protected override async Task<BufferedCommandResult> RunFFProbeCommand(Stream video, CancellationToken cancellationToken) =>
            // First try _asyncFuncWithToken, then _asyncFunc, then _func
            _asyncFuncWithToken != null
                ? await _asyncFuncWithToken(cancellationToken)
                : _asyncFunc != null ? await _asyncFunc() : _func!(Interlocked.Increment(ref _counter));
    }
}
