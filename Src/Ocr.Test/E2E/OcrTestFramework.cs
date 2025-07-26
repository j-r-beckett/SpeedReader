using System.Diagnostics.Metrics;
using Models;
using Ocr.Blocks;
using Ocr.Visualization;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Video;
using Video.Test;
using Xunit.Abstractions;

namespace Ocr.Test.E2E;

public record ExpectedText(Rectangle BoundingBox, string Text);

public record OcrTestResult(
    List<Rectangle> DetectedBoxes,
    List<string> RecognizedTexts,
    List<double> Confidences
);

public record OcrTestScenario(
    string Name,
    Image<Rgb24> Image,
    List<ExpectedText> ExpectedResults
);

public class OcrTestFramework
{
    private readonly Font _font;
    private readonly Font _smallFont;
    private readonly FileSystemUrlPublisher<OcrTestFramework> _imageSaver;

    public OcrTestFramework(ITestOutputHelper outputHelper)
    {
        // Font loading pattern from existing tests
        if (!SystemFonts.TryGet("Arial", out var fontFamily))
        {
            var defaultFontFamily = SystemFonts.Families.FirstOrDefault();
            if (defaultFontFamily != default)
            {
                fontFamily = defaultFontFamily;
            }
            else
            {
                throw new Exception("Failed to load font");
            }
        }

        _font = fontFamily.CreateFont(18);
        _smallFont = fontFamily.CreateFont(12);
        _imageSaver = new FileSystemUrlPublisher<OcrTestFramework>("/tmp", new TestLogger<OcrTestFramework>(outputHelper));
    }

    public async Task<OcrTestResult> RunOcrTest(OcrTestScenario scenario)
    {
        using var dbnetSession = ModelZoo.GetInferenceSession(Model.DbNet18);
        using var svtrSession = ModelZoo.GetInferenceSession(Model.SVTRv2);
        using var meter = new Meter("OcrTestFramework");

        var ocrBlock = OcrBlock.Create(dbnetSession, svtrSession, new OcrConfiguration(), meter);
        await using var bridge = new DataflowBridge<(Image<Rgb24>, VizBuilder), (Image<Rgb24>, OcrResult, VizBuilder)>(ocrBlock);

        var vizBuilder = VizBuilder.Create(VizMode.None, scenario.Image);
        var processTask = await bridge.ProcessAsync((scenario.Image, vizBuilder), CancellationToken.None, CancellationToken.None);

        var result = await processTask;

        // Extract results from OcrResult
        var detectedBoxes = new List<Rectangle>();
        var recognizedTexts = new List<string>();
        var confidences = new List<double>();

        var ocrResult = result.Item2;

        foreach (var word in ocrResult.Words)
        {
            var aaRect = word.BoundingBox.AARectangle;
            // Convert normalized coordinates to pixel coordinates
            var imageWidth = scenario.Image.Width;
            var imageHeight = scenario.Image.Height;
            var rect = new Rectangle(
                (int)(aaRect.X * imageWidth),
                (int)(aaRect.Y * imageHeight),
                (int)(aaRect.Width * imageWidth),
                (int)(aaRect.Height * imageHeight)
            );
            detectedBoxes.Add(rect);
            recognizedTexts.Add(word.Text);
            confidences.Add(word.Confidence);
        }

        var testResult = new OcrTestResult(
            detectedBoxes,
            recognizedTexts,
            confidences
        );

        // Always save visualization
        await SaveVisualization(scenario, testResult);

        return testResult;
    }

    private async Task SaveVisualization(OcrTestScenario scenario, OcrTestResult result)
    {
        var debugImage = scenario.Image.Clone(ctx =>
        {
            // Expected boxes in GREEN (dashed)
            foreach (var expected in scenario.ExpectedResults)
            {
                ctx.Draw(Pens.Dash(Color.Green, 2), expected.BoundingBox);
            }

            // Actual detection boxes in RED (solid)
            foreach (var actual in result.DetectedBoxes)
            {
                ctx.Draw(Pens.Solid(Color.Red, 2), actual);
            }

            // Expected text labels in GREEN
            foreach (var expected in scenario.ExpectedResults)
            {
                ctx.DrawText(expected.Text, _smallFont, Color.Green,
                    new PointF(expected.BoundingBox.X, expected.BoundingBox.Y - 20));
            }

            // Actual recognized text in BLUE
            for (int i = 0; i < result.RecognizedTexts.Count; i++)
            {
                if (i < result.DetectedBoxes.Count)
                {
                    ctx.DrawText(result.RecognizedTexts[i], _smallFont, Color.Blue,
                        new PointF(result.DetectedBoxes[i].X, result.DetectedBoxes[i].Bottom + 5));
                }
            }

            // Confidence scores in ORANGE
            for (int i = 0; i < result.Confidences.Count; i++)
            {
                if (i < result.DetectedBoxes.Count)
                {
                    ctx.DrawText($"{result.Confidences[i]:F2}", _smallFont, Color.Orange,
                        new PointF(result.DetectedBoxes[i].Right + 5, result.DetectedBoxes[i].Y));
                }
            }
        });

        await _imageSaver.PublishAsync(debugImage);
    }

    public void AssertDetectionAccuracy(OcrTestScenario scenario, OcrTestResult result)
    {
        if (scenario.ExpectedResults.Count != result.DetectedBoxes.Count)
        {
            var expectedTexts = string.Join(", ", scenario.ExpectedResults.Select(e => $"'{e.Text}'"));
            var detectedSummary = result.DetectedBoxes.Count == 0 ? "none" :
                string.Join(", ", result.DetectedBoxes.Select(b => $"({b.X},{b.Y}) {b.Width}x{b.Height}"));

            Assert.Fail(
                $"Detection count mismatch for scenario '{scenario.Name}'. " +
                $"Expected {scenario.ExpectedResults.Count} boxes for texts: {expectedTexts}. " +
                $"Found {result.DetectedBoxes.Count} boxes: {detectedSummary}.");
        }

        // Match detected boxes to expected boxes using existing logic pattern
        var remainingActuals = result.DetectedBoxes.ToList();
        foreach (var expected in scenario.ExpectedResults)
        {
            var closestActual = remainingActuals.MinBy(r => CalculateCloseness(r, expected.BoundingBox));
            remainingActuals.Remove(closestActual);

            var distance = CalculateCloseness(closestActual, expected.BoundingBox);
            var paddedActual = Pad(closestActual, 2);

            // Check that the actual box contains the expected box (with padding tolerance)
            if (!paddedActual.Contains(expected.BoundingBox))
            {
                Assert.Fail(
                    $"Detection containment failed for text '{expected.Text}' in scenario '{scenario.Name}'. " +
                    $"Expected box: ({expected.BoundingBox.X},{expected.BoundingBox.Y}) {expected.BoundingBox.Width}x{expected.BoundingBox.Height}. " +
                    $"Closest actual box: ({closestActual.X},{closestActual.Y}) {closestActual.Width}x{closestActual.Height}. " +
                    $"Distance: {distance} pixels. " +
                    $"Padded actual ({paddedActual.X},{paddedActual.Y}) {paddedActual.Width}x{paddedActual.Height} does not contain expected.");
            }

            // Check that the actual box is a reasonable size relative to expected
            if (closestActual.Width * 0.5 >= expected.BoundingBox.Width)
            {
                Assert.Fail(
                    $"Detection width too small for text '{expected.Text}' in scenario '{scenario.Name}'. " +
                    $"Expected width: {expected.BoundingBox.Width}, actual width: {closestActual.Width}. " +
                    $"Actual width must be at least 50% of expected ({expected.BoundingBox.Width * 0.5:F1}).");
            }

            if (closestActual.Height * 0.5 >= expected.BoundingBox.Height)
            {
                Assert.Fail(
                    $"Detection height too small for text '{expected.Text}' in scenario '{scenario.Name}'. " +
                    $"Expected height: {expected.BoundingBox.Height}, actual height: {closestActual.Height}. " +
                    $"Actual height must be at least 50% of expected ({expected.BoundingBox.Height * 0.5:F1}).");
            }
        }
    }

    public void AssertRecognitionAccuracy(OcrTestScenario scenario, OcrTestResult result)
    {
        var expectedTexts = scenario.ExpectedResults.Select(e => e.Text).ToList();
        var remainingActuals = result.RecognizedTexts.ToList();
        var matchedPairs = new List<(string Expected, string Actual)>();

        foreach (var expectedText in expectedTexts)
        {
            var matchFound = false;
            for (int i = remainingActuals.Count - 1; i >= 0; i--)
            {
                var actualText = new string(remainingActuals[i].Where(c => c <= 127).ToArray()).Trim();
                if (expectedText.Trim().Equals(actualText, StringComparison.OrdinalIgnoreCase))
                {
                    matchedPairs.Add((expectedText, actualText));
                    remainingActuals.RemoveAt(i);
                    matchFound = true;
                    break;
                }
            }

            if (!matchFound)
            {
                var cleanedActuals = result.RecognizedTexts
                    .Select(t => new string(t.Where(c => c <= 127).ToArray()).Trim())
                    .Where(t => !string.IsNullOrEmpty(t))
                    .ToList();

                var successfulMatches = matchedPairs.Count > 0 ?
                    $" Successfully matched: {string.Join(", ", matchedPairs.Select(p => $"'{p.Expected}'"))}." : "";

                var remainingExpected = expectedTexts.Skip(matchedPairs.Count).ToList();
                var unmatched = remainingExpected.Count > 1 ?
                    $" Still need to match: {string.Join(", ", remainingExpected.Skip(1).Select(t => $"'{t}'"))}." : "";

                Assert.Fail(
                    $"Recognition failed for text '{expectedText}' in scenario '{scenario.Name}'. " +
                    $"Expected: '{expectedText.Trim()}'. " +
                    $"Available actual texts: [{string.Join(", ", cleanedActuals.Select(t => $"'{t}'"))}]. " +
                    $"Remaining unmatched actuals: [{string.Join(", ", remainingActuals.Select(t => $"'{new string(t.Where(c => c <= 127).ToArray()).Trim()}'"))}].{successfulMatches}{unmatched}");
            }
        }
    }

    private static Rectangle Pad(Rectangle r, int p) => new(r.X - p, r.Y - p, r.Width + 2 * p, r.Height + 2 * p);

    private static int CalculateCloseness(Rectangle r1, Rectangle r2)
    {
        return EuclideanDistance(GetMidpoint(r1), GetMidpoint(r2));

        (int X, int Y) GetMidpoint(Rectangle r) => ((r.Left + r.Right) / 2, (r.Top + r.Bottom) / 2);

        int EuclideanDistance((int X, int Y) p1, (int X, int Y) p2) =>
            (int)Math.Ceiling(Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2)));
    }
}
