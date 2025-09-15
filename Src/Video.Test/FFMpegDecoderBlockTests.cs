// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TestUtils;
using Xunit.Abstractions;

namespace Video.Test;

public enum FramePattern
{
    Alternating,
    RedBlueBlocks
}

public class FFMpegDecoderBlockTests
{
    private const int Width = 100;
    private const int Height = 75;
    private readonly ILogger _logger;

    public FFMpegDecoderBlockTests(ITestOutputHelper outputHelper)
    {
        _logger = new TestLogger(outputHelper);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(10000)]
    public async Task CanDecodeRedBlueFrames(int totalFrames)
    {
        var videoStream = await CreateTestVideo(totalFrames, FramePattern.RedBlueBlocks, frameRate: 5);

        await WithVideoStream(videoStream, async stream =>
        {
            var decoder = new FfmpegDecoderBlockCreator();
            var sourceBlock = decoder.CreateFfmpegDecoderBlock(stream, 1, default);
            var frames = await CollectAllFrames(sourceBlock);

            await WithFrames(frames, frameList =>
            {
                Assert.Equal(totalFrames, frameList.Count);

                for (int i = 0; i < frameList.Count; i++)
                {
                    var blockIndex = i % 10;
                    var expectedColor = blockIndex < 5 ? Color.Red : Color.Blue;
                    var colorName = blockIndex < 5 ? "red" : "blue";

                    Assert.True(IsColorMatch(frameList[i], expectedColor), $"Frame {i} should be {colorName}");
                }
                return Task.CompletedTask;
            });

            foreach (var frame in frames)
            {
                frame.Dispose();
            }
        });
    }

    [Fact]
    public async Task BackpressureStopsInputConsumption()
    {
        var largeVideoStream = await CreateTestVideo(5000);
        var totalVideoSize = largeVideoStream.Length;

        _logger.LogInformation("Created large video: {totalSize} bytes", totalVideoSize);

        var decoder = new FfmpegDecoderBlockCreator();
        var sourceBlock = decoder.CreateFfmpegDecoderBlock(largeVideoStream, 1, default);

        // Wait for 1 second to allow some consumption
        await Task.Delay(1000);

        var consumedBytes1 = largeVideoStream.Position;

        // Verify that not all input was consumed
        Assert.True(consumedBytes1 > 0, "Expected some initial data consumption");
        Assert.True(consumedBytes1 < totalVideoSize,
            $"Expected backpressure to stop consumption, but entire video was consumed ({consumedBytes1}/{totalVideoSize} bytes)");

        // Wait another 500ms to verify consumption stays stable
        await Task.Delay(500);
        var consumedBytes2 = largeVideoStream.Position;

        Assert.Equal(consumedBytes1, consumedBytes2);

        var consumptionPercentage = (consumedBytes1 * 100.0) / totalVideoSize;

        _logger.LogInformation("Backpressure test passed - consumption stopped at {consumed} bytes ({percentage:F1}%) and stayed stable",
            consumedBytes1, consumptionPercentage);

        largeVideoStream.Dispose();
    }

    [Fact]
    public async Task BackpressureRespondsToFrameConsumption()
    {
        var videoStream = await CreateTestVideo(5760); // Target ~80% backpressure engagement for safety margin
        var totalVideoSize = videoStream.Length;

        try
        {
            var decoder = new FfmpegDecoderBlockCreator();
            var sourceBlock = decoder.CreateFfmpegDecoderBlock(videoStream, 1, default);

            // Wait for backpressure to engage
            await Task.Delay(300);
            var backpressurePosition = videoStream.Position;

            _logger.LogInformation("Backpressure engaged at {position} bytes ({percentage:F1}%)",
                backpressurePosition, (backpressurePosition * 100.0) / totalVideoSize);

            // Verify backpressure holds when no frames are consumed
            await Task.Delay(200);
            var stablePosition = videoStream.Position;

            Assert.Equal(backpressurePosition, stablePosition);

            var framesConsumed = 0;
            var backpressureReleased = false;

            // Consume all available frames
            while (await sourceBlock.OutputAvailableAsync())
            {
                var frame = await sourceBlock.ReceiveAsync();
                frame.Dispose();
                framesConsumed++;

                // Check for backpressure release periodically
                if (framesConsumed % 200 == 0 && !backpressureReleased)
                {
                    var currentPosition = videoStream.Position;

                    // Log only the first time backpressure is released
                    if (currentPosition > backpressurePosition)
                    {
                        _logger.LogInformation("Backpressure released at {frames} frames - stream advanced to {position} bytes", framesConsumed, currentPosition);
                        backpressureReleased = true;
                    }
                }
            }

            // Wait for source block to complete
            await sourceBlock.Completion;
            var finalPosition = videoStream.Position;

            // Verify backpressure was eventually released and flow resumed
            Assert.True(finalPosition > backpressurePosition);

            Assert.Equal(totalVideoSize, finalPosition);

            Assert.True(framesConsumed > 300);
        }
        finally
        {
            videoStream.Dispose();
        }
    }

    [Fact]
    public async Task HandlesCancellationGracefully()
    {
        var videoStream = await CreateTestVideo(50);
        videoStream.Position = 0;

        using var cts = new CancellationTokenSource();

        var decoder = new FfmpegDecoderBlockCreator();
        var sourceBlock = decoder.CreateFfmpegDecoderBlock(videoStream, 1, cts.Token);

        var frameCount = 0;
        var consumptionTask = Task.Run(async () =>
        {
            try
            {
                while (await sourceBlock.OutputAvailableAsync(cts.Token))
                {
                    var frame = await sourceBlock.ReceiveAsync(cts.Token);
                    frameCount++;

                    await Task.Delay(50, cts.Token);
                    frame.Dispose();

                    if (frameCount == 3)
                    {
                        cts.Cancel();
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        });

        await consumptionTask;

        Assert.Equal(3, frameCount);

        await Task.Delay(100);

        _logger.LogInformation("Cancellation test completed - processed {count} frames before clean shutdown", frameCount);

        videoStream.Dispose();
    }

    [Fact]
    public async Task FrameSamplingIsAccurate()
    {
        var videoStream = await CreateTestVideo(18, FramePattern.Alternating);

        await WithVideoStream(videoStream, async stream =>
        {
            var decoder = new FfmpegDecoderBlockCreator();
            var sourceBlock = decoder.CreateFfmpegDecoderBlock(stream, 2, default);
            var frames = await CollectAllFrames(sourceBlock);

            await WithFrames(frames, frameList =>
            {
                Assert.True(frameList.Count > 0);

                for (int i = 0; i < frameList.Count; i++)
                {
                    var isRed = IsColorMatch(frameList[i], Color.Red);
                    Assert.True(isRed, $"frame {i} should be red (sampled from position {i * 2} in original sequence)");
                }

                _logger.LogInformation("Frame sampling test passed - all {count} sampled frames were red as expected", frameList.Count);
                return Task.CompletedTask;
            });
        });
    }

    private async Task<Stream> CreateTestVideo(int frameCount, FramePattern pattern = FramePattern.Alternating, int width = Width, int height = Height, int frameRate = 10)
    {
        var videoStream = await FrameWriter.ToCompressedVideo(width, height, frameRate, GenerateFrames(frameCount, pattern, width, height).ToAsyncEnumerable(), default);
        _logger.LogInformation("Created test video: {frames} frames, {size} bytes", frameCount, videoStream.Length);
        return videoStream;
    }

    private IEnumerable<Image<Rgb24>> GenerateFrames(int frameCount, FramePattern pattern, int width = Width, int height = Height)
    {
        for (var i = 0; i < frameCount; i++)
        {
            var color = pattern switch
            {
                FramePattern.Alternating => i % 2 == 0 ? Color.Red : Color.Blue,
                FramePattern.RedBlueBlocks => (i % 10) < 5 ? Color.Red : Color.Blue,
                _ => throw new ArgumentOutOfRangeException(nameof(pattern))
            };
            yield return CreateImage(color, width, height);
        }
    }

    private Image<Rgb24> CreateImage(Color color, int width = Width, int height = Height)
    {
        var result = new Image<Rgb24>(width, height);

        result.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var pixelRow = accessor.GetRowSpan(y);
                for (var x = 0; x < pixelRow.Length; x++)
                {
                    pixelRow[x] = color;
                }
            }
        });

        return result;
    }

    private bool IsColorMatch(Image<Rgb24> image, Color expectedColor)
    {
        var expected = expectedColor.ToPixel<Rgb24>();
        var width = image.Width;
        var height = image.Height;

        var samplePoints = new[]
        {
            (width / 2, height / 2),
            (width / 4, height / 4),
            (3 * width / 4, height / 4),
            (width / 4, 3 * height / 4),
            (3 * width / 4, 3 * height / 4)
        };

        foreach (var (x, y) in samplePoints)
        {
            var actual = image[x, y];
            var error = Math.Abs(expected.R - actual.R) +
                       Math.Abs(expected.G - actual.G) +
                       Math.Abs(expected.B - actual.B);

            if (error >= 10)
            {
                return false;
            }
        }

        return true;
    }

    private async Task<List<Image<Rgb24>>> CollectAllFrames(ISourceBlock<Image<Rgb24>> sourceBlock)
    {
        var frames = new List<Image<Rgb24>>();
        while (await sourceBlock.OutputAvailableAsync())
        {
            var frame = await sourceBlock.ReceiveAsync();
            frames.Add(frame);
        }
        return frames;
    }

    private async Task WithVideoStream(Stream videoStream, Func<Stream, Task> action)
    {
        try
        {
            videoStream.Position = 0;
            await action(videoStream);
        }
        finally
        {
            videoStream.Dispose();
        }
    }

    private async Task WithFrames(List<Image<Rgb24>> frames, Func<List<Image<Rgb24>>, Task> action)
    {
        try
        {
            await action(frames);
        }
        finally
        {
            foreach (var frame in frames)
            {
                frame.Dispose();
            }
        }
    }
}
