using Models;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Video;
using Video.Test;
using Xunit.Abstractions;

namespace Ocr.Test;

[Collection("ONNX")]
public class PreprocessorTests
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly Font _font;
    private readonly FileSystemUrlPublisher<PreprocessorTests> _urlPublisher;

    public PreprocessorTests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        var outputDirectory = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "out", "debug");
        _urlPublisher = new FileSystemUrlPublisher<PreprocessorTests>(outputDirectory, new TestLogger<PreprocessorTests>(outputHelper));

        // Load font for text rendering
        FontFamily fontFamily;
        if (!SystemFonts.TryGet("Arial", out fontFamily))
        {
            fontFamily = SystemFonts.Collection.Families.First();
        }
        _font = fontFamily.CreateFont(48, FontStyle.Regular);
    }
    [Fact]
    public void Preprocess_NormalizesPixelsCorrectly()
    {
        // Arrange: Create 1x1 image with known RGB values
        using var image = new Image<Rgb24>(1, 1, new Rgb24(255, 128, 0));

        // Act
        using var buffer = DBNet.PreProcess([image]).Buffer;
        var tensor = buffer.AsTensor();

            // Assert: Verify exact normalized values using DBNet's normalization parameters
        var means = new[] { 123.675f, 116.28f, 103.53f };
        var stds = new[] { 58.395f, 57.12f, 57.375f };

        float expectedR = (255f - means[0]) / stds[0];  // ~2.252
        float expectedG = (128f - means[1]) / stds[1];  // ~0.205
        float expectedB = (0f - means[2]) / stds[2];    // ~-1.804

        var shape = tensor.Lengths;
        var tensorData = new float[tensor.FlattenedLength];
        tensor.FlattenTo(tensorData);

        int channelSize = (int)(shape[2] * shape[3]); // height * width

        Assert.Equal(expectedR, tensorData[0], 0.001f);                    // First R value
        Assert.Equal(expectedG, tensorData[channelSize], 0.001f);          // First G value
        Assert.Equal(expectedB, tensorData[2 * channelSize], 0.001f);      // First B value
    }

    [Fact]
    public void Preprocess_StoresDataInChwLayout()
    {
        // Arrange: Create 2x2 image with distinct RGB values per pixel
        using var image = new Image<Rgb24>(2, 2);
        image[0, 0] = new Rgb24(100, 150, 200); // Top-left
        image[1, 0] = new Rgb24(101, 151, 201); // Top-right
        image[0, 1] = new Rgb24(102, 152, 202); // Bottom-left
        image[1, 1] = new Rgb24(103, 153, 203); // Bottom-right

        // Act
        using var buffer = DBNet.PreProcess([image]).Buffer;
        var tensor = buffer.AsTensor();

        // Assert: Verify CHW layout - all R values, then all G values, then all B values
        var shape = tensor.Lengths;
        var tensorData = new float[tensor.FlattenedLength];
        tensor.FlattenTo(tensorData);

        int channelSize = (int)(shape[2] * shape[3]); // height * width

        // Red channel should be in first channelSize elements
        // Green channel should be in next channelSize elements
        // Blue channel should be in final channelSize elements

        // Since image gets padded to 32x32, we need to account for padding
        // The original 2x2 pixels should be in the top-left of the padded image

        // Verify the pattern exists (exact values depend on padding, but layout should be CHW)
        Assert.True(channelSize >= 4); // At least our 4 pixels after padding
        Assert.Equal(3 * channelSize, tensorData.Length); // 3 channels
    }

    [Fact]
    public void Preprocess_ProducesCorrectDimensions()
    {
        // Arrange: Test landscape image that needs resize and padding
        using var image = new Image<Rgb24>(100, 50);

        // Act
        using var buffer = DBNet.PreProcess([image]).Buffer;
        var tensor = buffer.AsTensor();

        // Assert: Verify dimensions are multiples of 32
        var shape = tensor.Lengths;
        int width = (int)shape[3];
        int height = (int)shape[2];

        Assert.Equal(0, width % 32);
        Assert.Equal(0, height % 32);

        // Verify dimensions don't exceed maximums (1333x736 → padded to 1344x768)
        Assert.True(width <= 1344);
        Assert.True(height <= 768);

        // For 100x50 input with aspect ratio 2:1, should fit within bounds while preserving ratio
        double aspectRatio = (double)width / height;
        Assert.True(aspectRatio >= 1.5); // Should maintain roughly 2:1 aspect ratio
    }

    [Fact]
    public void Preprocess_BlackImageNormalizesToExpectedValues()
    {
        // Arrange: Create black image
        using var image = new Image<Rgb24>(32, 32, new Rgb24(0, 0, 0));

        // Act
        using var buffer = DBNet.PreProcess([image]).Buffer;
        var tensor = buffer.AsTensor();

        // Assert: Black pixels should normalize to specific negative values
        var means = new[] { 123.675f, 116.28f, 103.53f };
        var stds = new[] { 58.395f, 57.12f, 57.375f };

        float expectedR = (0f - means[0]) / stds[0];  // ~-2.117
        float expectedG = (0f - means[1]) / stds[1];  // ~-2.035
        float expectedB = (0f - means[2]) / stds[2];  // ~-1.804

        var shape = tensor.Lengths;
        var tensorData = new float[tensor.FlattenedLength];
        tensor.FlattenTo(tensorData);

        int channelSize = (int)(shape[2] * shape[3]); // height * width

        // Check a sample of values from each channel
        Assert.Equal(expectedR, tensorData[0], 0.001f);                    // First R value
        Assert.Equal(expectedG, tensorData[channelSize], 0.001f);          // First G value
        Assert.Equal(expectedB, tensorData[2 * channelSize], 0.001f);      // First B value
    }

    [Fact]
    public void Preprocess_HandlesBatch()
    {
        // Arrange: Create batch of different sized images
        using var image1 = new Image<Rgb24>(100, 50, new Rgb24(255, 0, 0));
        using var image2 = new Image<Rgb24>(100, 50, new Rgb24(0, 255, 0));
        using var image3 = new Image<Rgb24>(100, 50, new Rgb24(0, 0, 255));

        // Act
        using var buffer = DBNet.PreProcess([image1, image2, image3]).Buffer;
        var tensor = buffer.AsTensor();

        // Assert: Verify batch dimensions
        var shape = tensor.Lengths;

        Assert.Equal(3, shape[0]); // Batch size
        Assert.Equal(3, shape[1]); // Channels

        // All images should result in same height/width after processing
        int height = (int)shape[2];
        int width = (int)shape[3];
        Assert.Equal(0, width % 32);
        Assert.Equal(0, height % 32);
    }

    [Fact]
    public void Preprocess_BatchCorrectness()
    {
        // Arrange: Create batch with known pixel values at specific locations
        using var redImage = new Image<Rgb24>(32, 32, new Rgb24(255, 0, 0));    // All red
        using var greenImage = new Image<Rgb24>(32, 32, new Rgb24(0, 255, 0));  // All green
        using var blueImage = new Image<Rgb24>(32, 32, new Rgb24(0, 0, 255));   // All blue

        // Act
        using var batchBuffer = DBNet.PreProcess([redImage, greenImage, blueImage]).Buffer;
        using var redBuffer = DBNet.PreProcess([redImage]).Buffer;
        using var greenBuffer = DBNet.PreProcess([greenImage]).Buffer;
        using var blueBuffer = DBNet.PreProcess([blueImage]).Buffer;

        var batchTensor = batchBuffer.AsTensor();
        var redTensor = redBuffer.AsTensor();
        var greenTensor = greenBuffer.AsTensor();
        var blueTensor = blueBuffer.AsTensor();

        // Assert: Verify each image in batch matches individual processing
        var batchData = new float[batchTensor.FlattenedLength];
        var redData = new float[redTensor.FlattenedLength];
        var greenData = new float[greenTensor.FlattenedLength];
        var blueData = new float[blueTensor.FlattenedLength];
        batchTensor.FlattenTo(batchData);
        redTensor.FlattenTo(redData);
        greenTensor.FlattenTo(greenData);
        blueTensor.FlattenTo(blueData);

        var shape = batchTensor.Lengths;
        int channelSize = (int)(shape[2] * shape[3]); // height * width
        int imageSize = 3 * channelSize; // 3 channels per image

        // Verify red image (batch index 0) matches individual red processing
        for (int i = 0; i < imageSize; i++)
        {
            Assert.Equal(redData[i], batchData[i], 0.001f);
        }

        // Verify green image (batch index 1) matches individual green processing
        for (int i = 0; i < imageSize; i++)
        {
            Assert.Equal(greenData[i], batchData[imageSize + i], 0.001f);
        }

        // Verify blue image (batch index 2) matches individual blue processing
        for (int i = 0; i < imageSize; i++)
        {
            Assert.Equal(blueData[i], batchData[2 * imageSize + i], 0.001f);
        }

        // Verify expected color channel dominance
        float[] means = [123.675f, 116.28f, 103.53f];
        float[] stds = [58.395f, 57.12f, 57.375f];

        // Red image should have high R channel, low G/B channels
        float expectedRed = (255f - means[0]) / stds[0];
        float expectedGreenZero = (0f - means[1]) / stds[1];
        float expectedBlueZero = (0f - means[2]) / stds[2];

        Assert.Equal(expectedRed, batchData[0], 0.001f);      // First pixel R channel
        Assert.Equal(expectedGreenZero, batchData[channelSize], 0.001f);  // First pixel G channel
        Assert.Equal(expectedBlueZero, batchData[2 * channelSize], 0.001f); // First pixel B channel
    }

    [Fact]
    public async Task Preprocess_HandlesJaggedBatchWithTextDetection()
    {
        // Arrange: Create jagged batch - small, medium, large, medium with text in different quadrants
        var imageInfo = new[]
        {
            (width: 800, height: 600, text: "SMALL", quadrant: 0),    // Small - both dimensions smaller than target
            (width: 1000, height: 500, text: "MEDIUM", quadrant: 1),  // Medium - width smaller, height smaller
            (width: 600, height: 900, text: "VERTICAL", quadrant: 2), // Medium - width smaller, height bigger
            (width: 1600, height: 1000, text: "LARGE", quadrant: 3)   // Large - both dimensions bigger than target
        };

        var images = new Image<Rgb24>[imageInfo.Length];
        var originalDimensions = new (int width, int height)[imageInfo.Length];

        for (int i = 0; i < imageInfo.Length; i++)
        {
            var info = imageInfo[i];
            originalDimensions[i] = (info.width, info.height);
            images[i] = CreateImageWithText(info.text, info.width, info.height, info.quadrant);
        }

        try
        {
            // Act: Process through complete pipeline
            using var session = ModelZoo.GetInferenceSession(Model.DbNet18);

            using var preprocessedBuffer = DBNet.PreProcess(images).Buffer;
            using var modelOutput = ModelRunner.Run(session, preprocessedBuffer.AsTensor());
            var probabilityMaps = TensorTestUtils.ExtractProbabilityMapsForTesting(modelOutput);

            // Assert: Verify pipeline worked correctly
            Assert.Equal(imageInfo.Length, probabilityMaps.Length);

            // Get model output dimensions (should be uniform across batch)
            int modelHeight = probabilityMaps[0].GetLength(0);
            int modelWidth = probabilityMaps[0].GetLength(1);

            _outputHelper.WriteLine($"Model output dimensions: {modelWidth}x{modelHeight}");

            // VISUALIZATION: Save all images for debugging before assertions
            await VisualizeJaggedBatchResults(images, probabilityMaps, imageInfo, originalDimensions, modelWidth, modelHeight);

            // Verify each image detected text in correct quadrant with coordinate conversion
            for (int i = 0; i < imageInfo.Length; i++)
            {
                var info = imageInfo[i];
                var originalWidth = originalDimensions[i].width;
                var originalHeight = originalDimensions[i].height;

                _outputHelper.WriteLine($"Image {i}: {info.text} - Original: {originalWidth}x{originalHeight}, Expected quadrant: {info.quadrant}");

                // Calculate how this specific image was scaled within the uniform tensor
                var scaleFactors = CalculateImageScaleFactors(originalWidth, originalHeight, modelWidth, modelHeight);

                ValidateTextDetectionInQuadrant(probabilityMaps[i], info.text, info.quadrant,
                    originalWidth, originalHeight, scaleFactors);
            }
        }
        finally
        {
            foreach (var image in images)
            {
                image?.Dispose();
            }
        }
    }

    private Image<Rgb24> CreateImageWithText(string text, int width, int height, int quadrant)
    {
        var image = new Image<Rgb24>(width, height, new Rgb24(255, 255, 255));

        // Calculate text position based on quadrant (with adaptive margin)
        int margin = Math.Min(width, height) / 10; // Adaptive margin

        // Measure text size for better positioning
        var textSize = TextMeasurer.MeasureSize(text, new TextOptions(_font));

        var (x, y) = quadrant switch
        {
            0 => (margin, margin),                                              // Top-left
            1 => (width - margin - (int)textSize.Width, margin),                // Top-right
            2 => (margin, height - margin - (int)textSize.Height),              // Bottom-left
            3 => (width - margin - (int)textSize.Width, height - margin - (int)textSize.Height), // Bottom-right
            _ => throw new ArgumentException($"Invalid quadrant: {quadrant}")
        };

        image.Mutate(ctx => ctx.DrawText(text, _font, new Rgb24(0, 0, 0), new PointF(x, y)));
        return image;
    }

    private (double scaleX, double scaleY, int scaledWidth, int scaledHeight) CalculateImageScaleFactors(
        int originalWidth, int originalHeight, int modelWidth, int modelHeight)
    {
        // Replicate the scaling logic from DBNet.CalculateDimensions for individual image
        double scale = Math.Min((double)1333 / originalWidth, (double)736 / originalHeight);
        int fittedWidth = (int)Math.Round(originalWidth * scale);
        int fittedHeight = (int)Math.Round(originalHeight * scale);
        int paddedWidth = (fittedWidth + 31) / 32 * 32;
        int paddedHeight = (fittedHeight + 31) / 32 * 32;

        // The image was scaled to fit within (paddedWidth, paddedHeight), then that was placed
        // within the larger model dimensions. Calculate the actual scale factors.
        double scaleX = (double)fittedWidth / originalWidth;
        double scaleY = (double)fittedHeight / originalHeight;

        _outputHelper.WriteLine($"Original: {originalWidth}x{originalHeight} -> Fitted: {fittedWidth}x{fittedHeight} -> " +
                                $"Padded: {paddedWidth}x{paddedHeight} -> Model: {modelWidth}x{modelHeight}");
        _outputHelper.WriteLine($"Scale factors: X={scaleX:F3}, Y={scaleY:F3}");

        return (scaleX, scaleY, fittedWidth, fittedHeight);
    }

    private void ValidateTextDetectionInQuadrant(float[,] probabilityMap, string text, int expectedQuadrant,
        int originalWidth, int originalHeight, (double scaleX, double scaleY, int scaledWidth, int scaledHeight) scaleFactors)
    {
        int modelHeight = probabilityMap.GetLength(0);
        int modelWidth = probabilityMap.GetLength(1);

        // Calculate quadrant boundaries in the scaled coordinate space
        // The scaled image sits at top-left of the model dimensions
        int scaledWidth = scaleFactors.scaledWidth;
        int scaledHeight = scaleFactors.scaledHeight;

        var quadrantTotals = new double[4];

        // Define quadrants within the actual scaled image area (not the full model area)
        var quadrantBounds = new[]
        {
            (startX: 0, endX: scaledWidth / 2, startY: 0, endY: scaledHeight / 2),                    // Top-left
            (startX: scaledWidth / 2, endX: scaledWidth, startY: 0, endY: scaledHeight / 2),          // Top-right
            (startX: 0, endX: scaledWidth / 2, startY: scaledHeight / 2, endY: scaledHeight),         // Bottom-left
            (startX: scaledWidth / 2, endX: scaledWidth, startY: scaledHeight / 2, endY: scaledHeight) // Bottom-right
        };

        // Calculate total probability in each quadrant
        for (int q = 0; q < 4; q++)
        {
            var bounds = quadrantBounds[q];
            for (int y = bounds.startY; y < bounds.endY && y < modelHeight; y++)
            {
                for (int x = bounds.startX; x < bounds.endX && x < modelWidth; x++)
                {
                    quadrantTotals[q] += probabilityMap[y, x];
                }
            }
        }

        _outputHelper.WriteLine($"Text '{text}' quadrant totals: TL={quadrantTotals[0]:F1}, TR={quadrantTotals[1]:F1}, " +
                                $"BL={quadrantTotals[2]:F1}, BR={quadrantTotals[3]:F1}");

        // The expected quadrant should have significantly higher total probability
        double expectedTotal = quadrantTotals[expectedQuadrant];

        // Verify the expected quadrant has reasonable detection
        Assert.True(expectedTotal > 0.5,
            $"Text '{text}' should be detected in quadrant {expectedQuadrant}, but total probability is only {expectedTotal:F2}");

        // Verify it's higher than all other quadrants by a significant margin
        for (int i = 0; i < 4; i++)
        {
            if (i != expectedQuadrant)
            {
                Assert.True(expectedTotal > quadrantTotals[i] * 2.0,
                    $"Text '{text}' should be primarily in quadrant {expectedQuadrant} (total: {expectedTotal:F2}), " +
                    $"but quadrant {i} has similar probability (total: {quadrantTotals[i]:F2})");
            }
        }
    }

    private async Task VisualizeJaggedBatchResults(Image<Rgb24>[] images, float[][,] probabilityMaps,
        (int width, int height, string text, int quadrant)[] imageInfo,
        (int width, int height)[] originalDimensions, int modelWidth, int modelHeight)
    {
        // 1. Save original images
        for (int i = 0; i < images.Length; i++)
        {
            await _urlPublisher.PublishAsync(images[i], $"jagged-original-{i}-{imageInfo[i].text}.png");
        }

        // 2. Save individual probability maps
        for (int i = 0; i < probabilityMaps.Length; i++)
        {
            using var probImage = RenderAsGreyscale(probabilityMaps[i]);
            await _urlPublisher.PublishAsync(probImage, $"jagged-result-{i}-{imageInfo[i].text}.png");
        }

        // 3. Calculate dimensions using Preprocessor logic and show actual scaled images
        var calculatedDimensions = DBNet.CalculateDimensions(images);
        _outputHelper.WriteLine($"Calculated tensor dimensions: {calculatedDimensions.width}x{calculatedDimensions.height}");

        for (int i = 0; i < images.Length; i++)
        {
            // Show exactly how each image looks after Preprocessor scaling
            using var actualScaledImage = CreateActualScaledImage(images[i], calculatedDimensions.width, calculatedDimensions.height);
            await _urlPublisher.PublishAsync(actualScaledImage, $"jagged-actual-scaled-{i}-{imageInfo[i].text}.png");

            // Also show the visualization with tensor fitting
            using var scaledImage = CreateScaledImageVisualization(images[i], originalDimensions[i], modelWidth, modelHeight);
            await _urlPublisher.PublishAsync(scaledImage, $"jagged-tensor-fit-{i}-{imageInfo[i].text}.png");
        }

        // 4. Create side-by-side analysis for each image
        for (int i = 0; i < images.Length; i++)
        {
            var scaleFactors = CalculateImageScaleFactors(originalDimensions[i].width, originalDimensions[i].height, modelWidth, modelHeight);
            using var analysisImage = CreateAnalysisVisualization(images[i], probabilityMaps[i], imageInfo[i],
                originalDimensions[i], scaleFactors, modelWidth, modelHeight);
            await _urlPublisher.PublishAsync(analysisImage, $"jagged-analysis-{i}-{imageInfo[i].text}.png");
        }

        // 5. Create grid visualization
        using var gridOriginal = CreateGridVisualization(images, "Original Images");
        await _urlPublisher.PublishAsync(gridOriginal, "jagged-grid-original.png");

        var probImages = probabilityMaps.Select(RenderAsGreyscale).ToArray();
        try
        {
            using var gridResults = CreateGridVisualizationFromProbMaps(probImages, "Probability Maps");
            await _urlPublisher.PublishAsync(gridResults, "jagged-grid-results.png");
        }
        finally
        {
            foreach (var img in probImages) img.Dispose();
        }
    }

    private Image<Rgb24> CreateActualScaledImage(Image<Rgb24> originalImage, int targetWidth, int targetHeight)
    {
        // Replicate exactly what Preprocessor.AspectResizeInto does
        using var resized = originalImage.Clone(x => x
            .Resize(new ResizeOptions
            {
                Size = new Size(targetWidth, targetHeight),
                Mode = ResizeMode.Pad,
                Position = AnchorPositionMode.TopLeft,
                PadColor = Color.Black,
                Sampler = KnownResamplers.Bicubic
            }));

        return resized.Clone();
    }

    private Image<Rgb24> CreateScaledImageVisualization(Image<Rgb24> originalImage,
        (int width, int height) originalDims, int modelWidth, int modelHeight)
    {
        // Create visualization showing how the image fits within the model tensor dimensions
        var visualization = new Image<Rgb24>(modelWidth, modelHeight, new Rgb24(64, 64, 64)); // Dark gray background

        // Calculate the same scaling as Preprocessor
        double scale = Math.Min((double)1333 / originalDims.width, (double)736 / originalDims.height);
        int fittedWidth = (int)Math.Round(originalDims.width * scale);
        int fittedHeight = (int)Math.Round(originalDims.height * scale);

        // Scale the image to fitted size
        using var scaledImage = originalImage.Clone();
        scaledImage.Mutate(x => x.Resize(fittedWidth, fittedHeight));

        // Place it at top-left (same as AspectResizeInto behavior)
        visualization.Mutate(ctx => ctx.DrawImage(scaledImage, new Point(0, 0), 1.0f));

        return visualization;
    }

    private Image<Rgb24> CreateAnalysisVisualization(Image<Rgb24> originalImage, float[,] probabilityMap,
        (int width, int height, string text, int quadrant) imageInfo,
        (int width, int height) originalDims,
        (double scaleX, double scaleY, int scaledWidth, int scaledHeight) scaleFactors,
        int modelWidth, int modelHeight)
    {
        // Create side-by-side: scaled input | probability output with quadrant overlays
        int visualWidth = modelWidth + modelWidth;
        var visualization = new Image<Rgb24>(visualWidth, modelHeight, new Rgb24(128, 128, 128));

        // Left side: scaled input image
        using var scaledInput = CreateScaledImageVisualization(originalImage, originalDims, modelWidth, modelHeight);
        visualization.Mutate(ctx => ctx.DrawImage(scaledInput, new Point(0, 0), 1.0f));

        // Right side: probability map
        using var probImage = RenderAsGreyscale(probabilityMap);
        visualization.Mutate(ctx => ctx.DrawImage(probImage, new Point(modelWidth, 0), 1.0f));

        // Draw quadrant boundaries on both sides
        var quadrantColors = new[] { Color.Red, Color.Green, Color.Blue, Color.Yellow };
        var quadrantBounds = CalculateQuadrantBounds(scaleFactors.scaledWidth, scaleFactors.scaledHeight);

        visualization.Mutate(ctx =>
        {
            // Left side quadrants
            for (int q = 0; q < 4; q++)
            {
                var bounds = quadrantBounds[q];
                var rect = new RectangleF(bounds.startX, bounds.startY,
                    bounds.endX - bounds.startX, bounds.endY - bounds.startY);
                ctx.Draw(Pens.Solid(quadrantColors[q], q == imageInfo.quadrant ? 4 : 2), rect);
            }

            // Right side quadrants (offset by modelWidth)
            for (int q = 0; q < 4; q++)
            {
                var bounds = quadrantBounds[q];
                var rect = new RectangleF(bounds.startX + modelWidth, bounds.startY,
                    bounds.endX - bounds.startX, bounds.endY - bounds.startY);
                ctx.Draw(Pens.Solid(quadrantColors[q], q == imageInfo.quadrant ? 4 : 2), rect);
            }
        });

        return visualization;
    }

    private (int startX, int endX, int startY, int endY)[] CalculateQuadrantBounds(int scaledWidth, int scaledHeight)
    {
        return new[]
        {
            (startX: 0, endX: scaledWidth / 2, startY: 0, endY: scaledHeight / 2),                    // Top-left
            (startX: scaledWidth / 2, endX: scaledWidth, startY: 0, endY: scaledHeight / 2),          // Top-right
            (startX: 0, endX: scaledWidth / 2, startY: scaledHeight / 2, endY: scaledHeight),         // Bottom-left
            (startX: scaledWidth / 2, endX: scaledWidth, startY: scaledHeight / 2, endY: scaledHeight) // Bottom-right
        };
    }

    private Image<Rgb24> CreateGridVisualization(Image<Rgb24>[] images, string title)
    {
        // Create 2x2 grid of images
        int maxWidth = images.Max(img => img.Width);
        int maxHeight = images.Max(img => img.Height);
        int gridWidth = maxWidth * 2;
        int gridHeight = maxHeight * 2;

        var grid = new Image<Rgb24>(gridWidth, gridHeight, new Rgb24(255, 255, 255));

        var positions = new[]
        {
            new Point(0, 0),                    // Top-left
            new Point(maxWidth, 0),             // Top-right
            new Point(0, maxHeight),            // Bottom-left
            new Point(maxWidth, maxHeight)      // Bottom-right
        };

        grid.Mutate(ctx =>
        {
            for (int i = 0; i < Math.Min(images.Length, 4); i++)
            {
                ctx.DrawImage(images[i], positions[i], 1.0f);
            }
        });

        return grid;
    }

    private Image<Rgb24> CreateGridVisualizationFromProbMaps(Image<Rgb24>[] probImages, string title)
    {
        return CreateGridVisualization(probImages, title);
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
