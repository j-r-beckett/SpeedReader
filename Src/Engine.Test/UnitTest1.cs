using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit.Abstractions;

namespace Engine.Test;

public class UnitTest1
{
    private const int Width = 100;
    private const int Height = 75;
    private readonly ILogger _logger;

    public UnitTest1(ITestOutputHelper outputHelper)
    {
        _logger = new TestLogger(outputHelper);
    }
    
    [Fact]
    public async Task Test1()
    {
        var channel = Channel.CreateUnbounded<Image<Rgb24>>();
        var writer = channel.Writer;
        
        // Write frames to channel
        for (int i = 0; i < 10; i++)
        {
            await writer.WriteAsync(CreateImage(Color.Red, Width, Height));
        }
        for (int i = 0; i < 10; i++)
        {
            await writer.WriteAsync(CreateImage(Color.Blue, Width, Height));
        }
        writer.Complete();
        
        _logger.LogInformation("Frames created");
        
        var videoStream = await FrameWriter.ToCompressedVideo(Width, Height, 5, channel, default);
        var savedPath = await videoStream.SaveVideoStream("webm", default);
        _logger.LogInformation("Saved video to {location}", savedPath);
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
}