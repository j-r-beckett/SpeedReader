using Video;
using Video.Test;
using Microsoft.Extensions.Logging;
using Models;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Xunit.Abstractions;

namespace Ocr.Test;

[Collection("ONNX")]
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

        // Use new 3-class flow: DBNet → ModelRunner → DBNet
        using var preprocessedBuffer = DBNet.PreProcess([image1, image2, image3, image4]);
        using var modelOutput = ModelRunner.Run(session, preprocessedBuffer.AsTensor());
        var probabilityMaps = TensorTestUtils.ExtractProbabilityMapsForTesting(modelOutput);

        // Save detection results for inspection
        using var result1 = RenderAsGreyscale(probabilityMaps[0]);
        using var result2 = RenderAsGreyscale(probabilityMaps[1]);
        using var result3 = RenderAsGreyscale(probabilityMaps[2]);
        using var result4 = RenderAsGreyscale(probabilityMaps[3]);

        await _urlPublisher.PublishAsync(result1, "batch-result-toplft.png");
        await _urlPublisher.PublishAsync(result2, "batch-result-toprgt.png");
        await _urlPublisher.PublishAsync(result3, "batch-result-botlft.png");
        await _urlPublisher.PublishAsync(result4, "batch-result-botrgt.png");

        // Validate that we get results for all images
        Assert.Equal(4, probabilityMaps.Length);

        // Validate each image detects text in the correct quadrant
        ValidateQuadrantDetection(probabilityMaps[0], "TOP", expectedQuadrant: 0);     // Top-left
        ValidateQuadrantDetection(probabilityMaps[1], "RGT", expectedQuadrant: 1);     // Top-right
        ValidateQuadrantDetection(probabilityMaps[2], "BOT", expectedQuadrant: 2);     // Bottom-left
        ValidateQuadrantDetection(probabilityMaps[3], "LFT", expectedQuadrant: 3);     // Bottom-right
    }

    private Image<Rgb24> CreateImageWithText(string text, (int row, int col) position)
    {
        var image = new Image<Rgb24>(100, 75, new Rgb24(255, 255, 255));
        image.Mutate(ctx => ctx.DrawText(text, _font, new Rgb24(0, 0, 0), new PointF(position.col, position.row)));
        return image;
    }

    private void ValidateQuadrantDetection(float[,] probabilityMap, string expectedText, int expectedQuadrant)
    {
        int height = probabilityMap.GetLength(0);
        int width = probabilityMap.GetLength(1);

        // Calculate total probability in each quadrant
        var quadrantTotals = new double[4];

        // Quadrant 0: Top-left
        quadrantTotals[0] = CalculateQuadrantTotal(probabilityMap, 0, height / 2, 0, width / 2);

        // Quadrant 1: Top-right  
        quadrantTotals[1] = CalculateQuadrantTotal(probabilityMap, 0, height / 2, width / 2, width);

        // Quadrant 2: Bottom-left
        quadrantTotals[2] = CalculateQuadrantTotal(probabilityMap, height / 2, height, 0, width / 2);

        // Quadrant 3: Bottom-right
        quadrantTotals[3] = CalculateQuadrantTotal(probabilityMap, height / 2, height, width / 2, width);

        var logger = new TestLogger<BatchProcessingTests>(_outputHelper);
        logger.LogInformation($"Text '{expectedText}' quadrant totals: TL={quadrantTotals[0]:F1}, TR={quadrantTotals[1]:F1}, BL={quadrantTotals[2]:F1}, BR={quadrantTotals[3]:F1}");

        // The expected quadrant should have significantly higher total probability than any other quadrant
        double expectedTotal = quadrantTotals[expectedQuadrant];

        // First ensure the expected quadrant has reasonable detection
        Assert.True(expectedTotal > 1.0, $"text '{expectedText}' should be detected in quadrant {expectedQuadrant}");

        // Then ensure it's at least 100x higher than any other quadrant
        for (int i = 0; i < 4; i++)
        {
            if (i != expectedQuadrant)
            {
                Assert.True((expectedTotal / Math.Max(quadrantTotals[i], 0.1)) > 100.0,
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

    private static Image<Rgb24> RenderAsGreyscale(float[,] probabilityMap)
    {
        int height = probabilityMap.GetLength(0);
        int width = probabilityMap.GetLength(1);

        var image = new Image<Rgb24>(width, height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float probability = probabilityMap[y, x];
                byte greyValue = (byte)(probability * 255f);
                image[x, y] = new Rgb24(greyValue, greyValue, greyValue);
            }
        }

        return image;
    }
}
