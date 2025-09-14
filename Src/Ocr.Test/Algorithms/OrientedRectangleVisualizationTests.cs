using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using TestUtils;
using Xunit.Abstractions;

namespace Ocr.Test.Algorithms;

/// <summary>
/// Visual tests for oriented rectangle test infrastructure.
/// These tests generate images that must be manually inspected to verify correctness.
/// </summary>
public class OrientedRectangleVisualizationTests
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly ILogger<OrientedRectangleVisualizationTests> _logger;
    private readonly FileSystemUrlPublisher<OrientedRectangleVisualizationTests> _publisher;

    public OrientedRectangleVisualizationTests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        _logger = new TestLogger<OrientedRectangleVisualizationTests>(outputHelper);
        var outputDirectory = "/tmp/oriented-rectangle-tests";
        _publisher = new FileSystemUrlPublisher<OrientedRectangleVisualizationTests>(outputDirectory, _logger);
    }

    [Fact]
    public async Task AxisAlignedOrientedRectangle_GeneratesCorrectGradients()
    {
        // Arrange - Create an axis-aligned rectangle
        var imageWidth = 400;
        var imageHeight = 300;
        var p = new PointF(100, 80);  // Top-left corner
        var q = new PointF(300, 80);  // Top-right corner (horizontal edge)
        var width = 120f; // Height of rectangle

        // Act - Generate test image
        using var image = OrientedRectangleTestUtils.CreateGradientOrientedRectangle(
            imageWidth, imageHeight, p, q, width);

        // Assert - Publish for visual inspection
        await _publisher.PublishAsync(image, "Axis-aligned oriented rectangle - should show blue background with red rectangle containing green origin marker");

        _outputHelper.WriteLine("Generated axis-aligned oriented rectangle test image");
        _outputHelper.WriteLine($"Expected: Blue background, red rectangle from ({p.X},{p.Y}) to ({q.X},{q.Y}), width {width}");
        _outputHelper.WriteLine("Gradients: Red should increase downward from p, Green should increase rightward from p");
        _outputHelper.WriteLine("Green square should mark the origin point p");
    }

    [Fact]
    public async Task UpwardTiltedOrientedRectangle_GeneratesCorrectGradients()
    {
        // Arrange - Create an upward-tilted rectangle
        var imageWidth = 400;
        var imageHeight = 300;
        var p = new PointF(50, 200);   // Bottom-left-ish
        var q = new PointF(350, 100);  // Top-right-ish (upward slope)
        var width = 60f; // Width perpendicular to p-q edge

        // Act - Generate test image
        using var image = OrientedRectangleTestUtils.CreateGradientOrientedRectangle(
            imageWidth, imageHeight, p, q, width);

        // Assert - Publish for visual inspection
        await _publisher.PublishAsync(image, "Upward-tilted oriented rectangle - should show tilted rectangle with origin marker");

        _outputHelper.WriteLine("Generated upward-tilted oriented rectangle test image");
        _outputHelper.WriteLine($"Expected: Blue background, tilted red rectangle from ({p.X},{p.Y}) to ({q.X},{q.Y}), width {width}");
        _outputHelper.WriteLine("Rectangle should tilt upward from left to right");
        _outputHelper.WriteLine("Green square should mark the origin point p at bottom-left of rectangle");
    }

    [Fact]
    public async Task DownwardTiltedOrientedRectangle_GeneratesCorrectGradients()
    {
        // Arrange - Create a downward-tilted rectangle
        var imageWidth = 400;
        var imageHeight = 300;
        var p = new PointF(50, 80);    // Top-left-ish
        var q = new PointF(350, 220);  // Bottom-right-ish (downward slope)
        var width = 60f; // Width perpendicular to p-q edge

        // Act - Generate test image
        using var image = OrientedRectangleTestUtils.CreateGradientOrientedRectangle(
            imageWidth, imageHeight, p, q, width);

        // Assert - Publish for visual inspection
        await _publisher.PublishAsync(image, "Downward-tilted oriented rectangle - should show tilted rectangle with origin marker");

        _outputHelper.WriteLine("Generated downward-tilted oriented rectangle test image");
        _outputHelper.WriteLine($"Expected: Blue background, tilted red rectangle from ({p.X},{p.Y}) to ({q.X},{q.Y}), width {width}");
        _outputHelper.WriteLine("Rectangle should tilt downward from left to right");
        _outputHelper.WriteLine("Green square should mark the origin point p at top-left of rectangle");
    }
}
