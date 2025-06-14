using Microsoft.Extensions.Logging;
using Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Video;
using Video.Test;
using Xunit;
using Xunit.Abstractions;

namespace Ocr.Test;

[Collection("ONNX")]
public class OcrEndToEndTests
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly TestLogger<OcrEndToEndTests> _logger;
    private readonly FileSystemUrlPublisher<OcrEndToEndTests> _urlPublisher;
    private readonly OcrValidator _ocrValidator;

    public OcrEndToEndTests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        _logger = new TestLogger<OcrEndToEndTests>(outputHelper);
        var outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "out", "debug");
        _urlPublisher = new FileSystemUrlPublisher<OcrEndToEndTests>(outputDirectory, _logger);
        _ocrValidator = new OcrValidator(outputHelper);
    }

    [Fact]
    public void BatchCheckeredPatternTest()
    {
        // Generate 4 medium-large images with checkered text patterns
        var imageSize = new Size(1200, 800);
        
        // Use similar proportions as ScaleVariationTest for consistency
        var boxWidth = imageSize.Width / 8;   // ~8 boxes across (safer spacing)
        var boxHeight = imageSize.Height / 16; // ~16 boxes down (shorter/wider boxes)
        var margin = Math.Min(boxWidth, boxHeight) / 6;
        
        var gridLayout = TextBoxLayouts.CreateGrid(4, 6, boxWidth, boxHeight, margin);

        var generatedImages = new GeneratedImage[]
        {
            TestImageGenerator.Generate(imageSize, gridLayout),
            TestImageGenerator.Generate(imageSize, gridLayout),
            TestImageGenerator.Generate(imageSize, gridLayout),
            TestImageGenerator.Generate(imageSize, gridLayout)
        };

        try
        {
            // Run full OCR pipeline on batch
            var results = RunOcrPipeline(generatedImages.Select(g => g.Image).ToArray());

            // Validate each image's results
            for (int i = 0; i < generatedImages.Length; i++)
            {
                var generated = generatedImages[i];
                var (detectedBoxes, recognizedTexts) = results[i];

                _logger.LogInformation($"Validating batch image {i}: {detectedBoxes.Length} detected, {recognizedTexts.Length} recognized");

                _ocrValidator.ValidateOcrResults(generated, recognizedTexts, detectedBoxes, gridLayout);
            }

            _logger.LogInformation($"✓ Batch checkered pattern test completed successfully for {generatedImages.Length} images");
        }
        finally
        {
            // Cleanup generated images
            foreach (var generated in generatedImages)
            {
                generated.Image.Dispose();
            }
        }
    }

    [Fact]
    public void ScaleVariationTest()
    {
        // Generate jagged batch with different image sizes
        var imageSizes = new[]
        {
            new Size(400, 300),   // Small
            new Size(800, 600),   // Medium
            new Size(1200, 900)   // Large
        };

        var generatedImages = new List<GeneratedImage>();

        try
        {
            // Create images with proportionally scaled grids
            foreach (var size in imageSizes)
            {
                // Scale grid dimensions proportionally to image size
                var boxWidth = size.Width / 6;   // ~6 boxes across
                var boxHeight = size.Height / 16; // ~8 boxes down (shorter/wider boxes for more chars)
                var margin = Math.Min(boxWidth, boxHeight) / 6;

                var gridLayout = TextBoxLayouts.CreateGrid(3, 3, boxWidth, boxHeight, margin);
                var generated = TestImageGenerator.Generate(size, gridLayout);
                generatedImages.Add(generated);
            }

            // Run full OCR pipeline on jagged batch
            var results = RunOcrPipeline(generatedImages.Select(g => g.Image).ToArray());

            // Validate each scale's results
            for (int i = 0; i < generatedImages.Count; i++)
            {
                var generated = generatedImages[i];
                var (detectedBoxes, recognizedTexts) = results[i];
                var imageSize = imageSizes[i];

                _logger.LogInformation($"Validating scale {imageSize}: {detectedBoxes.Length} detected, {recognizedTexts.Length} recognized");

                // Reconstruct the grid layout for this image size
                var boxWidth = imageSize.Width / 6;
                var boxHeight = imageSize.Height / 16;
                var margin = Math.Min(boxWidth, boxHeight) / 6;
                var expectedLayout = TextBoxLayouts.CreateGrid(3, 3, boxWidth, boxHeight, margin);

                _ocrValidator.ValidateOcrResults(generated, recognizedTexts, detectedBoxes, expectedLayout);
            }

            _logger.LogInformation($"✓ Scale variation test completed successfully for {imageSizes.Length} different scales");
        }
        finally
        {
            // Cleanup generated images
            foreach (var generated in generatedImages)
            {
                generated.Image.Dispose();
            }
        }
    }

    /// <summary>
    /// Lightweight shared utility for running the complete OCR pipeline on a batch of images
    /// and returning detection + recognition results for each image.
    /// </summary>
    private (Rectangle[] detectedBoxes, string[] recognizedTexts)[] RunOcrPipeline(Image<Rgb24>[] images)
    {
        using var dbnetSession = ModelZoo.GetInferenceSession(Model.DbNet18);
        using var svtrSession = ModelZoo.GetInferenceSession(Model.SVTRv2);

        // Step 1: Text Detection with DBNet
        using var preprocessed = Ocr.DBNet.PreProcess(images);
        using var detectionOutput = Ocr.ModelRunner.Run(dbnetSession, preprocessed.AsTensor());
        var detectedRectangles = Ocr.DBNet.PostProcess(detectionOutput, images);

        // Step 2: Text Recognition with SVTRv2
        using var textPreprocessed = Ocr.SVTRv2.PreProcess(images, detectedRectangles);
        using var recognitionOutput = Ocr.ModelRunner.Run(svtrSession, textPreprocessed.AsTensor());
        var allRecognizedTexts = Ocr.SVTRv2.PostProcess(recognitionOutput);

        // Package results per image
        var results = new (Rectangle[], string[])[images.Length];
        int textIndex = 0;

        for (int imageIndex = 0; imageIndex < images.Length; imageIndex++)
        {
            var detectedBoxes = detectedRectangles[imageIndex].ToArray();
            var recognizedTexts = new string[detectedBoxes.Length];

            // Extract recognition results for this image's detected boxes
            for (int boxIndex = 0; boxIndex < detectedBoxes.Length; boxIndex++)
            {
                recognizedTexts[boxIndex] = textIndex < allRecognizedTexts.Length
                    ? allRecognizedTexts[textIndex++]
                    : "";
            }

            results[imageIndex] = (detectedBoxes, recognizedTexts);
        }

        return results;
    }
}
