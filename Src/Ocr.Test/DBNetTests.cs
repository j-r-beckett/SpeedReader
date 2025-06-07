using CommunityToolkit.HighPerformance;
using Ocr;
using Xunit;

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
        var result = DBNet.PostProcess(buffer, originalWidth: 128, originalHeight: 128);
        
        // Current broken version: returns flat list of polygons (should be List<List<...>>)
        Assert.NotEmpty(result);
        
        // The detected polygon should contain points in the text region
        var polygon = result[0];
        Assert.True(polygon.Count >= 3); // Valid polygon
        
        // All points should be within image bounds
        foreach (var point in polygon)
        {
            Assert.InRange(point.X, 0, 127);
            Assert.InRange(point.Y, 0, 127);
        }
        
        // The polygon should roughly encompass the text region (56-72 range)
        var minX = polygon.Min(p => p.X);
        var maxX = polygon.Max(p => p.X);
        var minY = polygon.Min(p => p.Y);
        var maxY = polygon.Max(p => p.Y);
        
        // Should be roughly in the center area (allowing for dilation)
        Assert.InRange(minX, 40, 65);
        Assert.InRange(maxX, 63, 88);
        Assert.InRange(minY, 40, 65);
        Assert.InRange(maxY, 63, 88);
    }
}