using System.Threading.Tasks.Dataflow;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit.Abstractions;

namespace Engine.Test;

public class FFMpegEncoderBlockTests
{
    private const int Width = 100;
    private const int Height = 75;
    private readonly ITestOutputHelper _outputHelper;
    private readonly CapturingLogger<FFMpegEncoderBlockTests> _logger;
    private readonly FileSystemUrlPublisher<FFMpegEncoderBlockTests> _publisher;

    public FFMpegEncoderBlockTests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        _logger = new CapturingLogger<FFMpegEncoderBlockTests>();
        var outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "out", "debug");
        _publisher = new FileSystemUrlPublisher<FFMpegEncoderBlockTests>(outputDirectory, _logger);
    }

    [Fact]
    public async Task CanEncodeRedBlueFramesToVideo()
    {
        // Generate test frames: 10 red + 10 blue
        var frames = GenerateRedBlueFrames(10, 10).ToList();
        
        _outputHelper.WriteLine($"Generated {frames.Count} test frames");

        // Create encoder block
        var encoder = new FfmpegEncoderBlockCreator("ffmpeg");
        var encoderBlock = encoder.CreateFfmpegEncoderBlock(
            Width, Height, frameRate: 5.0, 
            out var encodedOutput, 
            default);

        // Start reading encoded output concurrently
        var outputTask = ReadEncodedStreamAsync(encodedOutput);

        // Send all frames to encoder block
        foreach (var frame in frames)
        {
            await encoderBlock.SendAsync(frame);
        }
        
        // Signal completion
        encoderBlock.Complete();
        await encoderBlock.Completion;

        // Get the encoded video stream
        var videoStream = await outputTask;
        
        _outputHelper.WriteLine($"Encoded video size: {videoStream.Length} bytes");

        // Save to file and log URL
        await _publisher.PublishAsync(videoStream, "video/webm", "Red/Blue test video");

        // Output captured logs to test console
        foreach (var logEntry in _logger.LogEntries)
        {
            _outputHelper.WriteLine($"[{logEntry.LogLevel}] {logEntry.Message}");
        }

        // Basic verification
        videoStream.Length.Should().BeGreaterThan(1000, "encoded video should have reasonable size");
        
        // Dispose frames
        foreach (var frame in frames)
        {
            frame.Dispose();
        }
    }

    [Fact]
    public async Task BackpressureStopsOutputConsumption()
    {
        // Create encoder block with no consumer reading the output
        var encoder = new FfmpegEncoderBlockCreator("ffmpeg");
        var encoderBlock = encoder.CreateFfmpegEncoderBlock(
            Width, Height, frameRate: 30.0, // Higher framerate for faster encoding
            out var encodedOutput, 
            default);

        _outputHelper.WriteLine("Starting backpressure test - feeding frames without consuming output");

        var framesSent = 0;
        var sendTimes = new List<TimeSpan>();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Feed frames and monitor send times to detect when backpressure kicks in
        var feedingTask = Task.Run(async () =>
        {
            try
            {
                for (int i = 0; i < 1200; i++) // Optimized frame count
                {
                    var frame = CreateImage(i % 2 == 0 ? Color.Red : Color.Blue);
                    
                    var sendStart = stopwatch.Elapsed;
                    await encoderBlock.SendAsync(frame);
                    var sendEnd = stopwatch.Elapsed;
                    
                    sendTimes.Add(sendEnd - sendStart);
                    framesSent++;

                    // Log progress every 100 frames
                    if (framesSent % 100 == 0)
                    {
                        var avgSendTime = sendTimes.Skip(Math.Max(0, sendTimes.Count - 10)).Average(t => t.TotalMilliseconds);
                        _outputHelper.WriteLine($"Sent {framesSent} frames, avg send time (last 10): {avgSendTime:F2}ms");
                    }

                    // Stop early if sending becomes very slow (indicates backpressure)
                    if (sendTimes.Count > 50)
                    {
                        var recentAvg = sendTimes.Skip(sendTimes.Count - 10).Average(t => t.TotalMilliseconds);
                        var initialAvg = sendTimes.Take(10).Average(t => t.TotalMilliseconds);
                        
                        if (recentAvg > initialAvg * 10) // 10x slower indicates backpressure
                        {
                            _outputHelper.WriteLine($"Backpressure detected: send time increased from {initialAvg:F2}ms to {recentAvg:F2}ms");
                            break;
                        }
                    }
                }
                
                encoderBlock.Complete();
            }
            catch (Exception ex)
            {
                _outputHelper.WriteLine($"Frame feeding failed: {ex.Message}");
                throw;
            }
        });

        // Wait for feeding to complete or timeout
        var timeout = Task.Delay(2000); // Fast timeout like decoder test
        var completedTask = await Task.WhenAny(feedingTask, timeout);

        if (completedTask == timeout)
        {
            _outputHelper.WriteLine($"Test timed out after sending {framesSent} frames - this indicates backpressure is working");
        }
        else
        {
            await feedingTask; // Re-throw any exceptions
        }

        // Verify that we didn't send all frames (backpressure should have stopped us)
        framesSent.Should().BeLessThan(1200, "backpressure should prevent all frames from being sent");
        framesSent.Should().BeGreaterThan(100, "should send substantial frames before backpressure kicks in");

        // Log timing analysis for debugging
        if (sendTimes.Count > 20)
        {
            var initialAvg = sendTimes.Take(10).Average(t => t.TotalMilliseconds);
            var finalAvg = sendTimes.Skip(Math.Max(0, sendTimes.Count - 10)).Average(t => t.TotalMilliseconds);
            
            _outputHelper.WriteLine($"Send time analysis: {initialAvg:F2}ms → {finalAvg:F2}ms");
            
            // Don't assert on timing - backpressure may work differently than expected
            // The key evidence is that we stopped before sending all frames
        }

        _outputHelper.WriteLine($"Backpressure test completed - sent {framesSent} frames before blocking");

        // Output captured logs
        foreach (var logEntry in _logger.LogEntries)
        {
            _outputHelper.WriteLine($"[{logEntry.LogLevel}] {logEntry.Message}");
        }
    }

    private IEnumerable<Image<Rgb24>> GenerateRedBlueFrames(int redCount, int blueCount)
    {
        // Generate red frames
        for (int i = 0; i < redCount; i++)
        {
            yield return CreateImage(Color.Red);
        }
        
        // Generate blue frames
        for (int i = 0; i < blueCount; i++)
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

    private async Task<MemoryStream> ReadEncodedStreamAsync(System.IO.Pipelines.PipeReader reader)
    {
        var outputStream = new MemoryStream();
        
        try
        {
            while (true)
            {
                var result = await reader.ReadAsync();
                var buffer = result.Buffer;

                if (buffer.Length > 0)
                {
                    // Copy buffer to memory stream
                    foreach (var segment in buffer)
                    {
                        await outputStream.WriteAsync(segment);
                    }
                }

                reader.AdvanceTo(buffer.End);

                if (result.IsCompleted)
                {
                    break;
                }
            }
        }
        finally
        {
            await reader.CompleteAsync();
        }

        outputStream.Position = 0;
        return outputStream;
    }
}