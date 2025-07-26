using System.Collections.Concurrent;
using CommunityToolkit.HighPerformance;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Ocr.Visualization;

public class DiagnosticVizBuilder : BasicVizBuilder
{
    private List<Rectangle> _rectangles = new();
    private readonly ConcurrentBag<(string text, Rectangle bounds)> _individualRecognitionResults = new();
    private Image<L8>? _probabilityMap;
    private List<List<(int X, int Y)>> _detectionPolygons = [];

    public DiagnosticVizBuilder(Image<Rgb24> sourceImage) : base(sourceImage) { }

    public override void AddRectangles(List<Rectangle> rectangles)
    {
        _rectangles = rectangles;
    }

    public override void AddProbabilityMap(Span2D<float> probabilityMap)
    {
        // The probability map is at preprocessed size, not original size
        var height = probabilityMap.Height;
        var width = probabilityMap.Width;

        // Calculate the fitted dimensions (what the image was resized to before padding)
        var originalWidth = _sourceImage.Width;
        var originalHeight = _sourceImage.Height;
        double scale = Math.Min((double)1333 / originalWidth, (double)736 / originalHeight);
        int fittedWidth = (int)Math.Round(originalWidth * scale);
        int fittedHeight = (int)Math.Round(originalHeight * scale);

        // Create a grayscale image from the probability map (only the fitted portion, not padding)
        var probImage = new Image<L8>(fittedWidth, fittedHeight);

        for (int y = 0; y < fittedHeight; y++)
        {
            for (int x = 0; x < fittedWidth; x++)
            {
                var probability = probabilityMap[y, x];
                // Convert probability [0,1] to grayscale [0,255]
                probImage[x, y] = new L8((byte)(probability * 255));
            }
        }

        // Resize back to original image size
        _probabilityMap = probImage.Clone(ctx =>
            ctx.Resize(originalWidth, originalHeight, KnownResamplers.Bicubic));

        probImage.Dispose();
    }

    public override void AddRecognitionResult(string text, TextBoundary boundary)
    {
        _individualRecognitionResults.Add((text, boundary.AARectangle));
    }

    public override void AddPolygons(List<List<(int X, int Y)>> polygons)
    {
        _detectionPolygons = polygons;
    }

    public override Image<Rgb24> Render()
    {
        // Start with the basic visualization
        var result = RenderBasicViz();

        var smallFont = Resources.Fonts.GetFont(fontSize: 10f, fontStyle: FontStyle.Regular);

        result.Mutate(ctx =>
        {
            // 1. Draw probability map as semi-transparent overlay
            if (_probabilityMap != null)
            {
                // Convert grayscale probability map to RGBA with transparency
                using var overlayImage = new Image<Rgba32>(_probabilityMap.Width, _probabilityMap.Height);

                for (int y = 0; y < _probabilityMap.Height; y++)
                {
                    for (int x = 0; x < _probabilityMap.Width; x++)
                    {
                        var intensity = _probabilityMap[x, y].PackedValue;
                        // Create semi-transparent yellow overlay (higher probability = more opaque)
                        var alpha = (byte)(intensity / 2); // Max 50% opacity
                        // Yellow = full red + full green, no blue
                        overlayImage[x, y] = new Rgba32(255, 255, 0, alpha);
                    }
                }

                // Draw the probability map overlay
                ctx.DrawImage(overlayImage, 1.0f);
            }

            // 2. Draw detection polygons in purple
            foreach (var polygon in _detectionPolygons)
            {
                if (polygon.Count >= 3) // Need at least 3 points for a polygon
                {
                    var points = polygon.Select(p => new PointF(p.X, p.Y)).ToArray();
                    var path = new Polygon(new LinearLineSegment(points));
                    ctx.Draw(Pens.Solid(Color.Purple, 1), path);
                }
            }

            // 3. Draw unmerged bounding boxes in green
            foreach (var rect in _rectangles)
            {
                var boundingRect = new RectangleF(rect.X, rect.Y, rect.Width, rect.Height);
                ctx.Draw(Pens.Solid(Color.Green, 1), boundingRect);
            }

            // 4. Draw individual recognition results from concurrent processing in blue
            foreach (var (text, bounds) in _individualRecognitionResults)
            {
                if (!string.IsNullOrEmpty(text.Trim()))
                {
                    // Position text below the rectangle
                    var textPosition = new PointF(bounds.X, bounds.Bottom + 2);

                    // Draw with white background for readability
                    var textSize = TextMeasurer.MeasureAdvance(text.Trim(), new TextOptions(smallFont));
                    var backgroundRect = new RectangleF(textPosition.X - 1, textPosition.Y - 1,
                        textSize.Width + 2, textSize.Height + 2);
                    ctx.Fill(Color.White.WithAlpha(0.8f), backgroundRect);

                    // Draw the text in blue to distinguish from batch results
                    ctx.DrawText(text.Trim(), smallFont, Color.Blue, textPosition);
                }
            }
        });

        return result;
    }
}
