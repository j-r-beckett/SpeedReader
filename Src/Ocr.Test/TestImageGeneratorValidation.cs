using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.Fonts;
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
            new Rectangle(0, 0, 200, 60),
            
            // Exact center
            new Rectangle((imageWidth - 200) / 2, (imageHeight - 60) / 2, 200, 60),
            
            // Bottom-right corner (box ends at image edge)
            new Rectangle(imageWidth - 200, imageHeight - 60, 200, 60)
        };
        
        // Generate image
        var result = TestImageGenerator.Generate(new Size(imageWidth, imageHeight), textBoxes);
        
        // Save and log
        var filename = $"step1-basic-{DateTime.Now:yyyyMMdd-HHmmss}.png";
        await _urlPublisher.PublishAsync(result.Image, filename);
        
        _logger.LogInformation($"Generated test image with {textBoxes.Length} boxes at positions:");
        _logger.LogInformation($"  Top-left corner: {textBoxes[0]}");
        _logger.LogInformation($"  Exact center: {textBoxes[1]}");
        _logger.LogInformation($"  Bottom-right corner: {textBoxes[2]}");
        
        // Cleanup
        result.Image.Dispose();
    }
    
    [Fact]
    public async Task Step2_FontSizeCalculation()
    {
        // Create boxes with different heights to test font scaling
        var textBoxes = new[]
        {
            new Rectangle(50, 50, 200, 30),    // Small height: 30px → 24pt font
            new Rectangle(50, 150, 200, 60),   // Medium height: 60px → 48pt font
            new Rectangle(50, 300, 200, 120)   // Large height: 120px → 96pt font
        };
        
        // Generate image
        var result = TestImageGenerator.Generate(new Size(400, 500), textBoxes);
        
        // Save and log
        var filename = $"step2-font-sizing-{DateTime.Now:yyyyMMdd-HHmmss}.png";
        await _urlPublisher.PublishAsync(result.Image, filename);
        
        _logger.LogInformation("Generated test image with varied height boxes:");
        _logger.LogInformation("  Box heights: 30px, 60px, 120px → Font sizes: 24pt, 48pt, 96pt");
        foreach (var (box, i) in textBoxes.Select((b, i) => (b, i)))
        {
            var fontSize = box.Height * 0.8f;
            _logger.LogInformation($"  Box {i}: Height={box.Height}px, Font size={fontSize:F1}pt");
        }
        
        // Cleanup
        result.Image.Dispose();
    }
    
    [Fact]
    public async Task Step3_WordSelection()
    {
        // Create boxes with different widths to test word selection
        var textBoxes = new[]
        {
            new Rectangle(50, 50, 100, 60),    // Narrow box - should fit short words
            new Rectangle(50, 150, 300, 60),   // Wide box - should fit longer words
            new Rectangle(50, 250, 500, 60)    // Very wide box - should fit longest words
        };
        
        // Generate image
        var result = TestImageGenerator.Generate(new Size(600, 400), textBoxes);
        
        // Save and log
        var filename = $"step3-word-selection-{DateTime.Now:yyyyMMdd-HHmmss}.png";
        await _urlPublisher.PublishAsync(result.Image, filename);
        
        _logger.LogInformation("Generated test image with varied width boxes:");
        _logger.LogInformation("  Narrow box (100px): Should show short word");
        _logger.LogInformation("  Wide box (300px): Should show medium word");
        _logger.LogInformation("  Very wide box (500px): Should show long word");
        
        // Cleanup
        result.Image.Dispose();
    }
    
    [Fact]
    public async Task Step4_UniqueDigitSuffixes()
    {
        // Create boxes with varied widths to show text filling
        var textBoxes = new[]
        {
            new Rectangle(50, 50, 150, 60),    // Narrow - will fit short words
            new Rectangle(250, 50, 250, 60),   // Medium - will fit medium words
            new Rectangle(50, 150, 350, 60),   // Wide - will fit longer words
            new Rectangle(50, 250, 450, 60),   // Very wide - will fit longest words
        };
        
        // Generate image
        var result = TestImageGenerator.Generate(new Size(600, 400), textBoxes);
        
        // Save and log
        var filename = $"step4-unique-text-{DateTime.Now:yyyyMMdd-HHmmss}.png";
        await _urlPublisher.PublishAsync(result.Image, filename);
        
        _logger.LogInformation($"Generated test image with {textBoxes.Length} boxes of varying widths");
        _logger.LogInformation("Text should fill boxes better with appropriate word selection");
        
        // Log the box widths
        _logger.LogInformation("Box widths: 150px (narrow), 250px (medium), 350px (wide), 450px (very wide)");
        
        // Cleanup
        result.Image.Dispose();
    }
    
    [Fact]
    public async Task Step5_ExactBoundsTracking()
    {
        // Create boxes to demonstrate exact bounds tracking
        var textBoxes = new[]
        {
            new Rectangle(50, 50, 300, 80),    // Large hint box
            new Rectangle(50, 150, 200, 60),   // Medium hint box
            new Rectangle(50, 250, 150, 40),   // Small hint box
        };
        
        // Generate image
        var result = TestImageGenerator.Generate(new Size(500, 400), textBoxes);
        
        // Save and log
        var filename = $"step5-exact-bounds-{DateTime.Now:yyyyMMdd-HHmmss}.png";
        await _urlPublisher.PublishAsync(result.Image, filename);
        
        _logger.LogInformation("Generated test image showing hint boxes (gray) and actual text bounds (red)");
        _logger.LogInformation("Rendered texts with exact bounds:");
        
        for (int i = 0; i < result.RenderedTexts.Length; i++)
        {
            var rendered = result.RenderedTexts[i];
            var hint = textBoxes[i];
            _logger.LogInformation($"  Text {i}: '{rendered.Text}' in hint box {hint}");
        }
        
        // Cleanup
        result.Image.Dispose();
    }
}