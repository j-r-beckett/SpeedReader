using Resources;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Xunit.Abstractions;

namespace Ocr.Test.E2E;

public class OcrE2ETests
{
    private readonly OcrTestFramework _framework;
    private readonly Font _testFont;

    public OcrE2ETests(ITestOutputHelper outputHelper)
    {
        _framework = new OcrTestFramework(outputHelper);
        _testFont = GetTestFont();
    }

    private static Font GetTestFont()
    {
        return Fonts.GetFont(fontSize: 24f);
    }

    private Rectangle DrawText(Image image, string text, int x, int y)
    {
        image.Mutate(ctx => ctx.DrawText(text, _testFont, Color.Black, new PointF(x, y)));
        var textRect = TextMeasurer.MeasureAdvance(text, new TextOptions(_testFont));
        return new Rectangle(x, y, (int)Math.Ceiling(textRect.Width), (int)Math.Ceiling(textRect.Height));
    }

    [Fact]
    public async Task BasicTextLayout()
    {
        // Arrange
        var image = new Image<Rgb24>(1200, 800, Color.White);
        var expectedResults = new List<ExpectedText>();

        // Basic scattered text
        expectedResults.Add(new ExpectedText(DrawText(image, "hello", 100, 100), "hello"));
        expectedResults.Add(new ExpectedText(DrawText(image, "world", 300, 150), "world"));
        expectedResults.Add(new ExpectedText(DrawText(image, "quick", 800, 100), "quick"));
        expectedResults.Add(new ExpectedText(DrawText(image, "brown", 1000, 200), "brown"));
        expectedResults.Add(new ExpectedText(DrawText(image, "fox", 150, 500), "fox"));
        expectedResults.Add(new ExpectedText(DrawText(image, "jumps", 250, 650), "jumps"));
        expectedResults.Add(new ExpectedText(DrawText(image, "over", 900, 550), "over"));
        expectedResults.Add(new ExpectedText(DrawText(image, "lazy", 800, 700), "lazy"));
        expectedResults.Add(new ExpectedText(DrawText(image, "dog", 600, 400), "dog"));

        var scenario = new OcrTestScenario("BasicLayout", image, expectedResults);

        // Act
        var result = await _framework.RunOcrTest(scenario);

        // Assert
        _framework.AssertDetectionAccuracy(scenario, result);
        _framework.AssertRecognitionAccuracy(scenario, result);
    }

    [Fact]
    public async Task EdgePositions()
    {
        // Arrange
        var image = new Image<Rgb24>(1200, 800, Color.White);
        var expectedResults = new List<ExpectedText>();

        // Edge positions
        expectedResults.Add(new ExpectedText(DrawText(image, "top", 600, 2), "top"));
        expectedResults.Add(new ExpectedText(DrawText(image, "right", 1150, 400), "right"));
        expectedResults.Add(new ExpectedText(DrawText(image, "bottom", 600, 780), "bottom"));
        expectedResults.Add(new ExpectedText(DrawText(image, "left", 2, 400), "left"));
        expectedResults.Add(new ExpectedText(DrawText(image, "topleft", 2, 2), "topleft"));
        expectedResults.Add(new ExpectedText(DrawText(image, "topright", 1120, 2), "topright"));
        expectedResults.Add(new ExpectedText(DrawText(image, "bottomright", 1090, 780), "bottomright"));
        expectedResults.Add(new ExpectedText(DrawText(image, "bottomleft", 2, 780), "bottomleft"));

        var scenario = new OcrTestScenario("EdgePositions", image, expectedResults);

        // Act
        var result = await _framework.RunOcrTest(scenario);

        // Assert
        _framework.AssertDetectionAccuracy(scenario, result);
        _framework.AssertRecognitionAccuracy(scenario, result);
    }

    [Fact]
    public async Task SquareAspectRatio()
    {
        // Arrange
        var image = new Image<Rgb24>(800, 800, Color.White);
        var expectedResults = new List<ExpectedText>();

        // Square image (1:1 aspect ratio)
        expectedResults.Add(new ExpectedText(DrawText(image, "square", 100, 100), "square"));
        expectedResults.Add(new ExpectedText(DrawText(image, "center", 350, 350), "center"));
        expectedResults.Add(new ExpectedText(DrawText(image, "corner", 650, 650), "corner"));
        expectedResults.Add(new ExpectedText(DrawText(image, "side", 100, 650), "side"));
        expectedResults.Add(new ExpectedText(DrawText(image, "edge", 650, 100), "edge"));

        var scenario = new OcrTestScenario("SquareAspectRatio", image, expectedResults);

        // Act
        var result = await _framework.RunOcrTest(scenario);

        // Assert
        _framework.AssertDetectionAccuracy(scenario, result);
        _framework.AssertRecognitionAccuracy(scenario, result);
    }

    [Fact]
    public async Task WideAspectRatio()
    {
        // Arrange
        var image = new Image<Rgb24>(1200, 400, Color.White);
        var expectedResults = new List<ExpectedText>();

        // Wide rectangle (3:1 aspect ratio)
        expectedResults.Add(new ExpectedText(DrawText(image, "wide", 100, 100), "wide"));
        expectedResults.Add(new ExpectedText(DrawText(image, "long", 400, 200), "long"));
        expectedResults.Add(new ExpectedText(DrawText(image, "stretch", 900, 100), "stretch"));
        expectedResults.Add(new ExpectedText(DrawText(image, "span", 300, 300), "span"));
        expectedResults.Add(new ExpectedText(DrawText(image, "broad", 600, 300), "broad"));

        var scenario = new OcrTestScenario("WideAspectRatio", image, expectedResults);

        // Act
        var result = await _framework.RunOcrTest(scenario);

        // Assert
        _framework.AssertDetectionAccuracy(scenario, result);
        _framework.AssertRecognitionAccuracy(scenario, result);
    }

    [Fact]
    public async Task TallAspectRatio()
    {
        // Arrange
        var image = new Image<Rgb24>(400, 1200, Color.White);
        var expectedResults = new List<ExpectedText>();

        // Tall rectangle (1:3 aspect ratio)
        expectedResults.Add(new ExpectedText(DrawText(image, "tall", 100, 100), "tall"));
        expectedResults.Add(new ExpectedText(DrawText(image, "thin", 200, 300), "thin"));
        expectedResults.Add(new ExpectedText(DrawText(image, "high", 100, 600), "high"));
        expectedResults.Add(new ExpectedText(DrawText(image, "long", 200, 900), "long"));
        expectedResults.Add(new ExpectedText(DrawText(image, "slim", 100, 1100), "slim"));

        var scenario = new OcrTestScenario("TallAspectRatio", image, expectedResults);

        // Act
        var result = await _framework.RunOcrTest(scenario);

        // Assert
        _framework.AssertDetectionAccuracy(scenario, result);
        _framework.AssertRecognitionAccuracy(scenario, result);
    }

    [Fact]
    public async Task AdjacentText()
    {
        // Arrange
        var image = new Image<Rgb24>(1200, 800, Color.White);
        var expectedResults = new List<ExpectedText>();

        // Adjacent text with varying proximities
        expectedResults.Add(new ExpectedText(DrawText(image, "hello", 100, 100), "hello"));
        expectedResults.Add(new ExpectedText(DrawText(image, "world", 200, 100), "world"));
        expectedResults.Add(new ExpectedText(DrawText(image, "hello", 100, 200), "hello"));
        expectedResults.Add(new ExpectedText(DrawText(image, "world", 175, 200), "world"));
        expectedResults.Add(new ExpectedText(DrawText(image, "hello", 95, 300), "hello"));
        expectedResults.Add(new ExpectedText(DrawText(image, "world", 155, 300), "world"));
        expectedResults.Add(new ExpectedText(DrawText(image, "goodbye", 500, 100), "goodbye"));
        expectedResults.Add(new ExpectedText(DrawText(image, "planet", 500, 200), "planet"));
        expectedResults.Add(new ExpectedText(DrawText(image, "goodbye", 600, 100), "goodbye"));
        expectedResults.Add(new ExpectedText(DrawText(image, "planet", 600, 150), "planet"));
        expectedResults.Add(new ExpectedText(DrawText(image, "goodbye", 700, 100), "goodbye"));
        expectedResults.Add(new ExpectedText(DrawText(image, "planet", 700, 125), "planet"));

        var scenario = new OcrTestScenario("AdjacentText", image, expectedResults);

        // Act
        var result = await _framework.RunOcrTest(scenario);

        // Assert
        _framework.AssertDetectionAccuracy(scenario, result);
        _framework.AssertRecognitionAccuracy(scenario, result);
    }

    [Fact]
    public async Task RecognitionVocabulary()
    {
        // Arrange
        var image = new Image<Rgb24>(1000, 600, Color.White);
        var expectedResults = new List<ExpectedText>();

        // Diverse vocabulary test
        expectedResults.Add(new ExpectedText(DrawText(image, "quick", 50, 50), "quick"));
        expectedResults.Add(new ExpectedText(DrawText(image, "brown", 700, 80), "brown"));
        expectedResults.Add(new ExpectedText(DrawText(image, "fox", 300, 200), "fox"));
        expectedResults.Add(new ExpectedText(DrawText(image, "jumps", 150, 350), "jumps"));
        expectedResults.Add(new ExpectedText(DrawText(image, "over", 800, 300), "over"));
        expectedResults.Add(new ExpectedText(DrawText(image, "lazy", 400, 500), "lazy"));
        expectedResults.Add(new ExpectedText(DrawText(image, "dog", 100, 520), "dog"));
        expectedResults.Add(new ExpectedText(DrawText(image, "alphabet", 650, 450), "alphabet"));

        var scenario = new OcrTestScenario("RecognitionVocabulary", image, expectedResults);

        // Act
        var result = await _framework.RunOcrTest(scenario);

        // Assert
        _framework.AssertDetectionAccuracy(scenario, result);
        _framework.AssertRecognitionAccuracy(scenario, result);
    }

    [Fact(Skip = "Demo test for failure message validation")]
    public async Task FailureDemo()
    {
        // Arrange
        var image = new Image<Rgb24>(800, 600, Color.White);
        var expectedResults = new List<ExpectedText>();

        // Create image with "hello" and "world" but expect different text
        DrawText(image, "hello", 100, 100);
        DrawText(image, "world", 300, 150);

        // Expect wrong text to demonstrate failure messages
        expectedResults.Add(new ExpectedText(new Rectangle(100, 100, 50, 25), "goodbye"));
        expectedResults.Add(new ExpectedText(new Rectangle(300, 150, 60, 25), "planet"));
        expectedResults.Add(new ExpectedText(new Rectangle(500, 300, 40, 25), "missing")); // This text doesn't exist in image

        var scenario = new OcrTestScenario("FailureDemo", image, expectedResults);

        // Act
        var result = await _framework.RunOcrTest(scenario);
        
        // Assert
        _framework.AssertDetectionAccuracy(scenario, result);
        _framework.AssertRecognitionAccuracy(scenario, result);
    }
}