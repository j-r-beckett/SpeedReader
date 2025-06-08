using CommunityToolkit.HighPerformance;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ocr.Test;

public class DBNetTests
{
    [Fact]
    public void PostProcess_WithSimpleBinaryMap_ReturnsPolygons()
    {
        // Create a 128x128 binary probability map with a 16x16 text region
        var buffer = new Buffer<float>(1 * 128 * 128, [1, 128, 128]);
        var span = buffer.AsSpan();

        // Fill with zeros (background)
        span.Fill(0.0f);

        // Create a 16x16 text region in the center with values above threshold (0.2)
        var map = span.AsSpan2D(128, 128);
        for (int y = 56; y < 72; y++)
        {
            for (int x = 56; x < 72; x++)
            {
                map[y, x] = 0.8f;  // Text pixel
            }
        }

        // Test with 1:1 scaling (model size = original size)
        var testImage = new Image<Rgb24>(128, 128);
        var result = DBNet.PostProcess(buffer, [testImage]);
        testImage.Dispose();

        // Should return array of batches, each containing list of rectangles
        Assert.NotEmpty(result);
        Assert.NotEmpty(result[0]);

        // The detected rectangle should be valid
        var rectangle = result[0][0];
        Assert.True(rectangle.Width > 0);
        Assert.True(rectangle.Height > 0);

        // Rectangle should be within image bounds
        Assert.InRange(rectangle.X, 0, 127);
        Assert.InRange(rectangle.Y, 0, 127);
        Assert.InRange(rectangle.X + rectangle.Width, 0, 128);
        Assert.InRange(rectangle.Y + rectangle.Height, 0, 128);

        // The rectangle should roughly encompass the text region (56-72 range)
        // Should be roughly in the center area (allowing for dilation)
        Assert.InRange(rectangle.X, 40, 65);
        Assert.InRange(rectangle.X + rectangle.Width, 63, 88);
        Assert.InRange(rectangle.Y, 40, 65);
        Assert.InRange(rectangle.Y + rectangle.Height, 63, 88);
    }
}
