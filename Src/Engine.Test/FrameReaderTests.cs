using System.Threading.Channels;
using Engine;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit.Abstractions;

namespace Engine.Test;

public class FrameReaderTests
{
    private const int Width = 100;
    private const int Height = 75;
    private readonly ILogger _logger;

    public FrameReaderTests(ITestOutputHelper outputHelper)
    {
        _logger = new TestLogger(outputHelper);
    }
    
    [Fact]
    public async Task CanRoundTripRedBlueFrames()
    {
        // Create test video with red then blue frames
        var writeChannel = Channel.CreateUnbounded<Image<Rgb24>>();
        var writer = writeChannel.Writer;
        
        for (var i = 0; i < 5; i++)
        {
            await writer.WriteAsync(CreateImage(Color.Red, Width, Height));
        }
        for (var i = 0; i < 5; i++)
        {
            await writer.WriteAsync(CreateImage(Color.Blue, Width, Height));
        }
        writer.Complete();
        
        var videoStream = await FrameWriter.ToCompressedVideo(Width, Height, 5, writeChannel, default);
        videoStream.Position = 0;
        
        // Read frames back from video
        var readChannel = Channel.CreateUnbounded<Image<Rgb24>>();
        var frameReader = new FrameReader(_logger);
        
        var readTask = frameReader.ReadFramesAsync(videoStream, 5, readChannel, default);
        
        // Collect all frames
        var frames = new List<Image<Rgb24>>();
        await foreach (var frame in readChannel.Reader.ReadAllAsync())
        {
            frames.Add(frame);
        }
        
        await readTask;
        
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