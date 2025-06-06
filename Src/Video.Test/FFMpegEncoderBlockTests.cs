using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit.Abstractions;

namespace Video.Test;

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
        Assert.True(videoStream.Length > 1000);

        // Dispose frames
        foreach (var frame in frames)
        {
            frame.Dispose();
        }
    }

    [Fact]
    public async Task BackpressureStopsFrameAcceptance()
    {
        const int frameCount = 3000;

        // Create encoder block but don't consume output to trigger backpressure
        var encoder = new FfmpegEncoderBlockCreator("ffmpeg");
        var encoderBlock = encoder.CreateFfmpegEncoderBlock(
            Width, Height, frameRate: 30.0,
            out var encodedOutput,
            default);

        var framesSent = 0;
        var backpressureDetected = false;
        Task<bool>? blockedSendAsyncTask = null;

        _logger.LogInformation("Started feeding frames without consuming output");

        // Phase 1: Feed frames until SendAsync blocks (no output consumption)
        // Target ~1200 frames (80% of observed 1059 blocking point + safety margin)
        for (int i = 0; i < frameCount; i++)
        {
            var frame = CreateImage(i % 2 == 0 ? Color.Red : Color.Blue);

            // Start SendAsync but don't await it
            var sendAsyncTask = encoderBlock.SendAsync(frame);
            var delayTask = Task.Delay(300); // Fast 300ms timeout like decoder test

            var completedTask = await Task.WhenAny(sendAsyncTask, delayTask);

            if (completedTask == delayTask)
            {
                // SendAsync didn't complete in 300ms = backpressure detected!
                _logger.LogInformation("Backpressure detected at {frames} frames - SendAsync blocked", framesSent);
                backpressureDetected = true;
                blockedSendAsyncTask = sendAsyncTask;

                // Verify backpressure is sustained by testing another SendAsync call
                _logger.LogInformation("Verifying sustained backpressure with second SendAsync test");
                var secondFrame = CreateImage(Color.Green); // Different color for verification
                var secondSendAsyncTask = encoderBlock.SendAsync(secondFrame);
                var secondDelayTask = Task.Delay(200); // Fast 200ms verification timeout

                var secondCompletedTask = await Task.WhenAny(secondSendAsyncTask, secondDelayTask);

                if (secondCompletedTask == secondDelayTask)
                {
                    _logger.LogInformation("Sustained backpressure confirmed - second SendAsync also blocked");
                    // Keep the second blocked task to await later
                    blockedSendAsyncTask = secondSendAsyncTask; // Replace first with second blocked task
                }
                else
                {
                    _logger.LogWarning("Unexpected: second SendAsync completed quickly during backpressure");
                    await secondSendAsyncTask;
                    secondFrame.Dispose(); // Dispose second frame if completed quickly
                }

                // Dispose the original frame since we're breaking the loop
                frame.Dispose();
                break;
            }

            // SendAsync completed quickly, continue
            await sendAsyncTask; // Ensure it actually completed
            framesSent++;
            // Don't dispose frame here - ActionBlock handles disposal
        }

        // Verify backpressure was detected
        Assert.True(backpressureDetected);
        Assert.True(framesSent < frameCount);

        _logger.LogInformation("Backpressure engaged after {frames} frames", framesSent);

        // Phase 2: Start consuming output to release backpressure
        var outputTask = ReadEncodedStreamAsync(encodedOutput);

        // Phase 3: Complete the blocked SendAsync and continue with remaining frames
        if (blockedSendAsyncTask != null)
        {
            _logger.LogInformation("Waiting for blocked SendAsync to complete after output consumption started");
            await blockedSendAsyncTask; // This should now complete quickly
            framesSent++;
        }

        // Continue sending remaining frames (should be fast now)
        for (int i = framesSent; i < frameCount; i++)
        {
            var frame = CreateImage(i % 2 == 0 ? Color.Red : Color.Blue);
            await encoderBlock.SendAsync(frame);
            framesSent++;
            // Don't dispose frame here - ActionBlock handles disposal
        }

        encoderBlock.Complete();
        var videoStream = await outputTask;

        _logger.LogInformation("Encoding completed: {totalFrames} frames → {bytes} bytes", framesSent, videoStream.Length);

        // Save test output
        await _publisher.PublishAsync(videoStream, "video/webm", "Encoder backpressure test video");

        // Final assertions - verify full cycle completed
        Assert.Equal(frameCount, framesSent);
        Assert.True(videoStream.Length > 1000);

        // Output captured logs to test console
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
