using System.Threading.Tasks.Dataflow;
using Engine;
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
    
    [Fact]
    public async Task CanDecodeRedBlueFrames()
    {
        // Create test video with red then blue frames
        var videoStream = await FrameWriter.ToCompressedVideo(Width, Height, 5, RedBlueFrames().ToAsyncEnumerable(), default);
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
        Assert.Equal(10, frames.Count);
        
        // Check first 5 frames are red
        for (int i = 0; i < 5; i++)
        {
            Assert.True(IsColorMatch(frames[i], Color.Red), $"Frame {i} should be red");
        }
        
        // Check last 5 frames are blue
        for (int i = 5; i < 10; i++)
        {
            Assert.True(IsColorMatch(frames[i], Color.Blue), $"Frame {i} should be blue");
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
        var largeVideoStream = await CreateVeryLargeVideo();
        var totalVideoSize = largeVideoStream.Length;
        
        _logger.LogInformation("Created large video: {totalSize} bytes", totalVideoSize);
        
        // Create decoder but don't consume any frames (no consumer)
        var decoder = new FfmpegDecoderBlockCreator("ffmpeg");
        var sourceBlock = decoder.CreateFfmpegDecoderBlock(largeVideoStream, 1, default);
        
        // Wait for pipeline to fill up and stabilize
        await Task.Delay(3000);
        var consumedBytes1 = largeVideoStream.Position;
        
        _logger.LogInformation("After 3s: consumed {consumed} bytes out of {total} total bytes ({percentage:F1}%)", 
            consumedBytes1, totalVideoSize, (consumedBytes1 * 100.0) / totalVideoSize);
        
        // Wait more to verify consumption has stopped (backpressure is holding)
        await Task.Delay(5000);
        var consumedBytes2 = largeVideoStream.Position;
        
        _logger.LogInformation("After 8s total: consumed {consumed} bytes out of {total} total bytes ({percentage:F1}%)", 
            consumedBytes2, totalVideoSize, (consumedBytes2 * 100.0) / totalVideoSize);
        
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
        Assert.True(consumptionPercentage < 50, 
            $"Expected backpressure to limit consumption to <50%, but consumed {consumptionPercentage:F1}%");
        
        _logger.LogInformation("Backpressure test passed - consumption stopped at {consumed} bytes ({percentage:F1}%) and stayed stable", 
            consumedBytes1, consumptionPercentage);
        
        largeVideoStream.Dispose();
    }

    private async Task<Stream> CreateVeryLargeVideo()
    {
        // Create a video with many frames - much larger than pipeline buffers
        const int frameCount = 10000; // Much larger video
        
        // Use larger frame size to increase data volume
        return await FrameWriter.ToCompressedVideo(Width * 2, Height * 2, 30, LargeVideoFrames(frameCount).ToAsyncEnumerable(), default);
    }
    
    private IEnumerable<Image<Rgb24>> LargeVideoFrames(int frameCount)
    {
        for (var i = 0; i < frameCount; i++)
        {
            // Alternate between red and blue frames
            var color = i % 2 == 0 ? Color.Red : Color.Blue;
            yield return CreateImage(color, Width * 2, Height * 2);
        }
    }

    private IEnumerable<Image<Rgb24>> RedBlueFrames()
    {
        for (var i = 0; i < 5; i++)
        {
            yield return CreateImage(Color.Red);
        }
        for (var i = 0; i < 5; i++)
        {
            yield return CreateImage(Color.Blue);
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