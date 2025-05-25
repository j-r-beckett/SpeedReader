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