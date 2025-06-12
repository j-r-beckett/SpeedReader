using CommunityToolkit.HighPerformance;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ocr.Test;

public class DBNetTests_dirty
{
    [Fact]
    public void PostProcess_WithSimpleBinaryMap_ReturnsPolygons()
    {
        // Use an image size that results in 1:1 scaling with DBNet
        // DBNet fits images within 1333x736, so use 736x736 to get no scaling
        var testImage = new Image<Rgb24>(736, 736);

        // Create a buffer that matches the padded dimensions (must be divisible by 32)
        var paddedSize = 736; // Already divisible by 32
        var buffer = new Buffer<float>(1 * paddedSize * paddedSize, [1, paddedSize, paddedSize]);
        var span = buffer.AsSpan();

        // Fill with zeros (background)
        span.Fill(0.0f);

        // Create a text region in the expected position (scaled appropriately)
        var map = span.AsSpan2D(paddedSize, paddedSize);
        // Scale the original 56-72 region proportionally
        int startPos = (int)(56.0 / 128 * paddedSize);
        int endPos = (int)(72.0 / 128 * paddedSize);

        for (int y = startPos; y < endPos; y++)
        {
            for (int x = startPos; x < endPos; x++)
            {
                map[y, x] = 0.8f;  // Text pixel
            }
        }
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
        Assert.InRange(rectangle.X, 0, testImage.Width - 1);
        Assert.InRange(rectangle.Y, 0, testImage.Height - 1);
        Assert.InRange(rectangle.X + rectangle.Width, 0, testImage.Width);
        Assert.InRange(rectangle.Y + rectangle.Height, 0, testImage.Height);

        // The rectangle should roughly encompass the text region
        // Original region was 56-72 in 128x128, scale to 736x736
        int expectedStart = (int)(56.0 / 128 * 736);  // ~322
        int expectedEnd = (int)(72.0 / 128 * 736);    // ~414

        // Allow for dilation effects
        Assert.InRange(rectangle.X, expectedStart - 40, expectedStart + 10);
        Assert.InRange(rectangle.X + rectangle.Width, expectedEnd - 10, expectedEnd + 40);
        Assert.InRange(rectangle.Y, expectedStart - 40, expectedStart + 10);
        Assert.InRange(rectangle.Y + rectangle.Height, expectedEnd - 10, expectedEnd + 40);
    }
}
