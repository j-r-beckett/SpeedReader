using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;
using SixLabors.Fonts;
using Xunit;
using Xunit.Abstractions;

namespace Ocr.Test;

public class TestComparisonValidation
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly OcrValidator _ocrValidator;

    public TestComparisonValidation(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        _ocrValidator = new OcrValidator(outputHelper);
    }

    [Fact]
    public void OcrPipelineValidation()
    {
        // Create a 2x2 grid with real OCR pipeline - sized appropriately for text
        var grid = TextBoxLayouts.CreateGrid(2, 2, 80, 40, margin: 20);
        var imageSize = TextBoxLayouts.CalculateImageSize(2, 2, 80, 40, margin: 20);

        // Generate test image with known text
        var generated = TestImageGenerator.Generate(imageSize, grid);

        // Run actual OCR pipeline
        using var dbnetSession = Models.ModelZoo.GetInferenceSession(Models.Model.DbNet18);
        using var svtrSession = Models.ModelZoo.GetInferenceSession(Models.Model.SVTRv2);

        // Step 1: Text Detection with DBNet
        using var preprocessed = Ocr.DBNet.PreProcess([generated.Image]);
        using var detectionOutput = Ocr.ModelRunner.Run(dbnetSession, preprocessed.AsTensor());
        var detectedRectangles = Ocr.DBNet.PostProcess(detectionOutput, [generated.Image]);

        // Step 2: Text Recognition with SVTRv2
        using var textPreprocessed = Ocr.SVTRv2.PreProcess([generated.Image], detectedRectangles);
        using var recognitionOutput = Ocr.ModelRunner.Run(svtrSession, textPreprocessed.AsTensor());
        var recognizedTexts = Ocr.SVTRv2.PostProcess(recognitionOutput);

        // Create visualization with detected boxes and recognized text
        using var resultImage = generated.Image.Clone();
        var colors = new[] { Color.Purple, Color.Green, Color.Blue, Color.Orange };

        resultImage.Mutate(ctx =>
        {
            var firstBatchRectangles = detectedRectangles[0];
            for (int i = 0; i < firstBatchRectangles.Count && i < recognizedTexts.Length; i++)
            {
                var rectangle = firstBatchRectangles[i];
                var color = colors[i % colors.Length];
                var text = recognizedTexts[i];

                // Draw bounding rectangle
                ctx.Draw(Pens.Solid(color, 3), rectangle);

                // Draw recognized text above the box
                if (!string.IsNullOrWhiteSpace(text))
                {
                    var fontFamily = SystemFonts.TryGet("Arial", out var arial) ? arial : SystemFonts.Families.First();
                    var font = fontFamily.CreateFont(16);
                    ctx.DrawText(text, font, color, new PointF(rectangle.X, rectangle.Y - 20));
                }
            }
        });

        // Extract expected data for validation
        var hintBoxes = grid;
        var detectedBoxes = detectedRectangles[0].ToArray();

        // Unified validation with automatic debug image generation and detailed error messages
        _ocrValidator.ValidateOcrResults(generated, recognizedTexts, detectedBoxes, hintBoxes);
    }
}
