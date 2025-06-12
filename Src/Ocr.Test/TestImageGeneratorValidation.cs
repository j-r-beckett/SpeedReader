using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using Video;
using Video.Test;
using Xunit;
using Xunit.Abstractions;

namespace Ocr.Test;

public class TestImageGeneratorValidation
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly TestLogger<TestImageGeneratorValidation> _logger;
    private readonly FileSystemUrlPublisher<TestImageGeneratorValidation> _urlPublisher;

    public TestImageGeneratorValidation(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        _logger = new TestLogger<TestImageGeneratorValidation>(outputHelper);
        var outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "out", "debug");
        _urlPublisher = new FileSystemUrlPublisher<TestImageGeneratorValidation>(outputDirectory, _logger);
    }

    [Fact]
    public async Task Step1_BasicImageGeneration()
    {
        // Create 3 boxes at distinctive positions
        var imageWidth = 800;
        var imageHeight = 600;
        
        var textBoxes = new[]
        {
            // Top-left corner
            new TextBox(new Rectangle(0, 0, 200, 60)),
            
            // Exact center
            new TextBox(new Rectangle((imageWidth - 200) / 2, (imageHeight - 60) / 2, 200, 60)),
            
            // Bottom-right corner (box ends at image edge)
            new TextBox(new Rectangle(imageWidth - 200, imageHeight - 60, 200, 60))
        };
        
        // Generate image
        var image = TestImageGenerator.Generate(new Size(imageWidth, imageHeight), textBoxes);
        
        // Save and log
        var filename = $"step1-basic-{DateTime.Now:yyyyMMdd-HHmmss}.png";
        await _urlPublisher.PublishAsync(image, filename);
        
        _logger.LogInformation($"Generated test image with {textBoxes.Length} boxes at positions:");
        _logger.LogInformation($"  Top-left corner: {textBoxes[0].Bounds}");
        _logger.LogInformation($"  Exact center: {textBoxes[1].Bounds}");
        _logger.LogInformation($"  Bottom-right corner: {textBoxes[2].Bounds}");
        
        // Cleanup
        image.Dispose();
    }
}