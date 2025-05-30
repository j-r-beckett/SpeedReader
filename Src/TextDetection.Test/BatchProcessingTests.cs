using Engine;
using Engine.Test;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Models;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Xunit.Abstractions;

namespace TextDetection.Test;

public class BatchProcessingTests
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly Font _font;
    private readonly FileSystemUrlPublisher<BatchProcessingTests> _urlPublisher;

    public BatchProcessingTests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        var outputDirectory = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "out", "debug");
        _urlPublisher = new FileSystemUrlPublisher<BatchProcessingTests>(outputDirectory, new TestLogger<BatchProcessingTests>(outputHelper));

        // Load font for text rendering
        FontFamily fontFamily;
        if (!SystemFonts.TryGet("Arial", out fontFamily))
        {
            fontFamily = SystemFonts.Collection.Families.First();
        }
        _font = fontFamily.CreateFont(12, FontStyle.Regular);
    }

    [Fact]
    public async Task BatchProcessing_ProcessesAllImagesInBatch()
    {
        using var session = ModelZoo.GetInferenceSession(Model.DbNet18);
        using var detector = new TextDetector(session, new TestLogger<TextDetector>(_outputHelper));

        // Create images with words in different quadrants (3-letter codes, half dimensions)
        using var image1 = CreateImageWithText("TOP", (12, 19));       // Top-left quadrant
        using var image2 = CreateImageWithText("RGT", (12, 69));       // Top-right quadrant  
        using var image3 = CreateImageWithText("BOT", (56, 19));       // Bottom-left quadrant
        using var image4 = CreateImageWithText("LFT", (56, 69));       // Bottom-right quadrant

        // Save original images for inspection
        await _urlPublisher.PublishAsync(image1, "batch-original-toplft.png");
        await _urlPublisher.PublishAsync(image2, "batch-original-toprgt.png");
        await _urlPublisher.PublishAsync(image3, "batch-original-botlft.png");
        await _urlPublisher.PublishAsync(image4, "batch-original-botrgt.png");

        // Convert to DBNetImages
        var dbnetImage1 = DbNetImage.Create(image1);
        var dbnetImage2 = DbNetImage.Create(image2);
        var dbnetImage3 = DbNetImage.Create(image3);
        var dbnetImage4 = DbNetImage.Create(image4);

        // Process as batch
        using var batchInput = new TextDetectorInput(4, dbnetImage1.Height, dbnetImage1.Width);
        batchInput.LoadBatch(dbnetImage1, dbnetImage2, dbnetImage3, dbnetImage4);
        var batchOutputs = detector.RunTextDetection(batchInput);

        // Save detection results for inspection
        using var result1 = batchOutputs[0].RenderAsGreyscale();
        using var result2 = batchOutputs[1].RenderAsGreyscale();
        using var result3 = batchOutputs[2].RenderAsGreyscale();
        using var result4 = batchOutputs[3].RenderAsGreyscale();

        await _urlPublisher.PublishAsync(result1, "batch-result-toplft.png");
        await _urlPublisher.PublishAsync(result2, "batch-result-toprgt.png");
        await _urlPublisher.PublishAsync(result3, "batch-result-botlft.png");
        await _urlPublisher.PublishAsync(result4, "batch-result-botrgt.png");

        // Validate that we get results for all images
        batchOutputs.Length.Should().Be(4, "batch processing should return results for all images");

        // Validate each image detects text in the correct quadrant
        ValidateQuadrantDetection(batchOutputs[0], "TOP", expectedQuadrant: 0);     // Top-left
        ValidateQuadrantDetection(batchOutputs[1], "RGT", expectedQuadrant: 1);     // Top-right
        ValidateQuadrantDetection(batchOutputs[2], "BOT", expectedQuadrant: 2);     // Bottom-left
        ValidateQuadrantDetection(batchOutputs[3], "LFT", expectedQuadrant: 3);     // Bottom-right
    }

    private Image<Rgb24> CreateImageWithText(string text, (int row, int col) position)
    {
        var image = new Image<Rgb24>(100, 75, new Rgb24(255, 255, 255));
        image.Mutate(ctx => ctx.DrawText(text, _font, new Rgb24(0, 0, 0), new PointF(position.col, position.row)));
        return image;
    }

    private void ValidateQuadrantDetection(TextDetectorOutput output, string expectedText, int expectedQuadrant)
    {
        int height = output.ProbabilityMap.GetLength(0);
        int width = output.ProbabilityMap.GetLength(1);

        // Calculate total probability in each quadrant
        var quadrantTotals = new double[4];
        
        // Quadrant 0: Top-left
        quadrantTotals[0] = CalculateQuadrantTotal(output.ProbabilityMap, 0, height/2, 0, width/2);
        
        // Quadrant 1: Top-right  
        quadrantTotals[1] = CalculateQuadrantTotal(output.ProbabilityMap, 0, height/2, width/2, width);
        
        // Quadrant 2: Bottom-left
        quadrantTotals[2] = CalculateQuadrantTotal(output.ProbabilityMap, height/2, height, 0, width/2);
        
        // Quadrant 3: Bottom-right
        quadrantTotals[3] = CalculateQuadrantTotal(output.ProbabilityMap, height/2, height, width/2, width);

        var logger = new TestLogger<BatchProcessingTests>(_outputHelper);
        logger.LogInformation($"Text '{expectedText}' quadrant totals: TL={quadrantTotals[0]:F1}, TR={quadrantTotals[1]:F1}, BL={quadrantTotals[2]:F1}, BR={quadrantTotals[3]:F1}");

        // The expected quadrant should have significantly higher total probability than any other quadrant
        double expectedTotal = quadrantTotals[expectedQuadrant];
        
        // First ensure the expected quadrant has reasonable detection
        expectedTotal.Should().BeGreaterThan(1.0, $"text '{expectedText}' should be detected in quadrant {expectedQuadrant}");
        
        // Then ensure it's at least 100x higher than any other quadrant
        for (int i = 0; i < 4; i++)
        {
            if (i != expectedQuadrant)
            {
                (expectedTotal / Math.Max(quadrantTotals[i], 0.1)).Should().BeGreaterThan(100.0, 
                    $"text '{expectedText}' should be detected primarily in quadrant {expectedQuadrant}, but quadrant {i} has similar probability");
            }
        }
    }

    private static double CalculateQuadrantTotal(float[,] probabilityMap, int startRow, int endRow, int startCol, int endCol)
    {
        double sum = 0;
        
        for (int row = startRow; row < endRow; row++)
        {
            for (int col = startCol; col < endCol; col++)
            {
                sum += probabilityMap[row, col];
            }
        }
        
        return sum;
    }
}