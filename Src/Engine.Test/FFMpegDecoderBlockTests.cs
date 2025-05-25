using System.Threading.Tasks.Dataflow;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit.Abstractions;

namespace Engine.Test;

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
    [InlineData(10)]      // Original test case
    [InlineData(10000)]   // Large test case
    public async Task CanDecodeRedBlueFrames(int totalFrames)
    {
        // Create test video with red then blue frames (5 red, 5 blue pattern repeated)
        var videoStream = await FrameWriter.ToCompressedVideo(Width, Height, 5, RedBlueFrames(totalFrames).ToAsyncEnumerable(), default);
        videoStream.Position = 0;

        // Create decoder and decode frames
        var decoder = new FfmpegDecoderBlockCreator("ffmpeg");
        var sourceBlock = decoder.CreateFfmpegDecoderBlock(videoStream, 1, default);

        // Collect all frames
        var frames = new List<Image<Rgb24>>();
        while (await sourceBlock.OutputAvailableAsync())
        {
            var frame = await sourceBlock.ReceiveAsync();
            frames.Add(frame);
        }

        // Verify we got correct frames
        Assert.Equal(totalFrames, frames.Count);

        // Check pattern: first half red, second half blue (repeated in blocks of 10)
        for (int i = 0; i < frames.Count; i++)
        {
            var blockIndex = i % 10; // Position within each 10-frame block
            var expectedColor = blockIndex < 5 ? Color.Red : Color.Blue;
            var colorName = blockIndex < 5 ? "red" : "blue";
            
            Assert.True(IsColorMatch(frames[i], expectedColor), $"Frame {i} should be {colorName}");
        }

        // Cleanup
        foreach (var frame in frames)
        {
            frame.Dispose();
        }

        videoStream.Dispose();
    }

    [Fact]
    public async Task BackpressureStopsInputConsumption()
    {
        // Create a very large video - much larger than pipeline buffers can hold
        // Use larger frame size to increase data volume
        var largeVideoStream = await CreateTestVideo(2500);
        var totalVideoSize = largeVideoStream.Length;

        _logger.LogInformation("Created large video: {totalSize} bytes", totalVideoSize);

        // Create decoder but don't consume any frames (no consumer)
        var decoder = new FfmpegDecoderBlockCreator("ffmpeg");
        var sourceBlock = decoder.CreateFfmpegDecoderBlock(largeVideoStream, 1, default);

        var expectedConsumption = 131072;

        var timeout = Task.Delay(1000);
        while (largeVideoStream.Position < expectedConsumption && !timeout.IsCompleted)
        {
            await Task.Delay(10);
        }

        timeout.IsCompleted.Should().Be(false);

        // Wait for pipeline to fill up and stabilize
        // await Task.Delay(500);
        var consumedBytes1 = largeVideoStream.Position;


        // Wait more to verify consumption has stopped (backpressure is holding)
        await Task.Delay(500);
        var consumedBytes2 = largeVideoStream.Position;


        // Key assertions for backpressure:
        // 1. Some data was consumed (pipeline started)
        Assert.True(consumedBytes1 > 0, "Expected some initial data consumption");

        // 2. NOT all data was consumed (backpressure stopped FFmpeg)
        Assert.True(consumedBytes1 < totalVideoSize,
            $"Expected backpressure to stop consumption, but entire video was consumed ({consumedBytes1}/{totalVideoSize} bytes)");

        // 3. Consumption should have STOPPED (this is the key backpressure test)
        consumedBytes2.Should().Be(consumedBytes1,
            $"consumption should stop due to backpressure, but it increased from {consumedBytes1} to {consumedBytes2} bytes");

        // 4. Should have consumed significantly less than the total
        var consumptionPercentage = (consumedBytes1 * 100.0) / totalVideoSize;

        _logger.LogInformation("Backpressure test passed - consumption stopped at {consumed} bytes ({percentage:F1}%) and stayed stable",
            consumedBytes1, consumptionPercentage);

        largeVideoStream.Dispose();
    }

    [Fact]
    public async Task HandlesCancellationGracefully()
    {
        // Create a reasonably large video to ensure FFmpeg is running when we cancel
        var videoStream = await CreateTestVideo(50);
        videoStream.Position = 0;


        // Create cancellation token that will cancel mid-processing
        using var cts = new CancellationTokenSource();

        var decoder = new FfmpegDecoderBlockCreator("ffmpeg");
        var sourceBlock = decoder.CreateFfmpegDecoderBlock(videoStream, 1, cts.Token);

        // Start consuming frames
        var frameCount = 0;
        var consumptionTask = Task.Run(async () =>
        {
            try
            {
                while (await sourceBlock.OutputAvailableAsync(cts.Token))
                {
                    var frame = await sourceBlock.ReceiveAsync(cts.Token);
                    frameCount++;

                    // Add small delay to make cancellation timing more predictable
                    await Task.Delay(50, cts.Token);
                    frame.Dispose();

                    // Cancel after receiving a few frames (while FFmpeg is still running)
                    if (frameCount == 3)
                    {
                        cts.Cancel();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // This is expected
            }
        });

        // Wait for cancellation to propagate
        await consumptionTask;

        // Verify cancellation was handled gracefully
        frameCount.Should().Be(3, "should have processed exactly 3 frames before cancellation");

        // Give a moment for cleanup
        await Task.Delay(100);

        _logger.LogInformation("Cancellation test completed - processed {count} frames before clean shutdown", frameCount);

        videoStream.Dispose();
    }

    [Fact]
    public async Task FrameSamplingIsAccurate()
    {
        // Create video with alternating Red Blue Red Blue Red Blue...
        // Frames: 0=Red, 1=Blue, 2=Red, 3=Blue, 4=Red, 5=Blue...
        // With sampleRate=2, we should get frames 0, 2, 4, 6... (all Red frames)
        var videoStream = await CreateTestVideo(18);
        videoStream.Position = 0;

        // Request every 2nd frame (sampleRate=2)
        var decoder = new FfmpegDecoderBlockCreator("ffmpeg");
        var sourceBlock = decoder.CreateFfmpegDecoderBlock(videoStream, 2, default);

        // Collect all frames
        var frames = new List<Image<Rgb24>>();
        while (await sourceBlock.OutputAvailableAsync())
        {
            var frame = await sourceBlock.ReceiveAsync();
            frames.Add(frame);
        }


        // Verify we got some frames
        frames.Should().NotBeEmpty("should receive at least some sampled frames");

        // Verify ALL returned frames are red (since every 2nd frame starting from 0 is red)
        for (int i = 0; i < frames.Count; i++)
        {
            var isRed = IsColorMatch(frames[i], Color.Red);
            isRed.Should().BeTrue($"frame {i} should be red (sampled from position {i * 2} in original sequence)");
        }

        // Cleanup
        foreach (var frame in frames)
        {
            frame.Dispose();
        }

        _logger.LogInformation("Frame sampling test passed - all {count} sampled frames were red as expected", frames.Count);

        videoStream.Dispose();
    }

    private async Task<Stream> CreateTestVideo(int frameCount, int width = Width, int height = Height, int frameRate = 10)
    {
        var videoStream = await FrameWriter.ToCompressedVideo(width, height, frameRate, TestVideoFrames(frameCount, width, height).ToAsyncEnumerable(), default);
        _logger.LogInformation("Created test video: {frames} frames, {size} bytes", frameCount, videoStream.Length);
        return videoStream;
    }

    private IEnumerable<Image<Rgb24>> TestVideoFrames(int frameCount, int width, int height)
    {
        for (var i = 0; i < frameCount; i++)
        {
            // Alternate between red and blue frames
            var color = i % 2 == 0 ? Color.Red : Color.Blue;
            yield return CreateImage(color, width, height);
        }
    }

    private IEnumerable<Image<Rgb24>> RedBlueFrames(int totalFrames)
    {
        for (var i = 0; i < totalFrames; i++)
        {
            // Pattern: 5 red, 5 blue, repeat
            var blockIndex = i % 10;
            var color = blockIndex < 5 ? Color.Red : Color.Blue;
            yield return CreateImage(color);
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

        // Sample 5 points: center + 4 quadrants
        var samplePoints = new[]
        {
            (Width / 2, Height / 2), // Center
            (Width / 4, Height / 4), // Top-left quadrant
            (3 * Width / 4, Height / 4), // Top-right quadrant
            (Width / 4, 3 * Height / 4), // Bottom-left quadrant
            (3 * Width / 4, 3 * Height / 4) // Bottom-right quadrant
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
}