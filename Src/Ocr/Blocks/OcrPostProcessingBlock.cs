using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading.Tasks.Dataflow;
using Ocr.Visualization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ocr.Blocks;

public static class OcrPostProcessingBlock
{
    public static IPropagatorBlock<(Image<Rgb24>, List<TextBoundary>, List<string>, List<double>, VizBuilder), (Image<Rgb24>, OcrResult, VizBuilder)> Create(Meter meter)
    {
        var postProcessingCounter = meter.CreateCounter<long>("ocr_postprocessing_completed", description: "Number of completed OCR post-processing operations");

        return new TransformBlock<(Image<Rgb24> Image, List<TextBoundary> TextBoundaries, List<string> Texts, List<double> Confidences, VizBuilder VizBuilder), (Image<Rgb24>, OcrResult, VizBuilder)>(data =>
        {
            Debug.Assert(data.TextBoundaries.Count == data.Texts.Count);
            Debug.Assert(data.TextBoundaries.Count == data.Confidences.Count);

            // Extract rectangles for processing
            var rectangles = data.TextBoundaries.Select(tb => tb.AARectangle).ToList();

            // Step 1: Filter out empty text
            var (filteredTextBoundaries, filteredTexts, filteredConfidences) = FilterEmptyText(data.TextBoundaries, data.Texts, data.Confidences);
            var filteredRectangles = filteredTextBoundaries.Select(tb => tb.AARectangle).ToList();

            // Step 2: Create lines from words
            var lines = CreateLines(filteredRectangles, filteredTexts);

            // Step 3: Convert to OcrResult
            var ocrResults = ConvertToOcrResults(filteredTextBoundaries, filteredTexts, filteredConfidences, lines, data.Image);

            // Add merged results for visualization
            var mergedRectangles = lines.Select(line => line.bounds).ToList();
            var mergedTexts = lines.Select(line => line.text).ToList();
            data.VizBuilder.AddMergedResults(mergedRectangles, mergedTexts);

            postProcessingCounter.Add(1);
            return (data.Image, ocrResults, data.VizBuilder);
        }, new ExecutionDataflowBlockOptions
        {
            BoundedCapacity = Environment.ProcessorCount,
            MaxDegreeOfParallelism = Environment.ProcessorCount
        });
    }

    private static (List<TextBoundary>, List<string>, List<double>) FilterEmptyText(List<TextBoundary> textBoundaries, List<string> texts, List<double> confidences)
    {
        var filteredTextBoundaries = new List<TextBoundary>();
        var filteredTexts = new List<string>();
        var filteredConfidences = new List<double>();

        for (int i = 0; i < texts.Count; i++)
        {
            var cleanedText = new string(texts[i].Where(c => c <= 127).ToArray()).Trim();
            if (cleanedText != string.Empty)
            {
                filteredTextBoundaries.Add(textBoundaries[i]);
                filteredTexts.Add(cleanedText);
                filteredConfidences.Add(confidences[i]);
            }
        }

        return (filteredTextBoundaries, filteredTexts, filteredConfidences);
    }


    private static List<(string text, Rectangle bounds, List<int> wordIndices)> CreateLines(List<Rectangle> rectangles, List<string> texts)
    {
        if (rectangles.Count == 0)
            return new List<(string, Rectangle, List<int>)>();

        // Build undirected adjacency list for connected components
        var adjacency = new List<List<int>>();
        for (int i = 0; i < rectangles.Count; i++)
        {
            adjacency.Add([]);
        }

        // Create edges (O(n^2))
        for (int i = 0; i < rectangles.Count; i++)
        {
            for (int j = i + 1; j < rectangles.Count; j++)
            {
                if (CanMerge(rectangles[i], rectangles[j]))
                {
                    adjacency[i].Add(j);
                    adjacency[j].Add(i);
                }
            }
        }

        // Find connected components using DFS
        var visited = new bool[rectangles.Count];
        var components = new List<List<int>>();

        for (int i = 0; i < rectangles.Count; i++)
        {
            if (!visited[i])
            {
                var component = new List<int>();
                DFS(i, adjacency, visited, component);
                components.Add(component);
            }
        }

        // Create lines from components
        var lines = new List<(string text, Rectangle bounds, List<int> wordIndices)>();
        foreach (var component in components)
        {
            // Sort component by X position for proper text ordering
            component.Sort((a, b) => rectangles[a].X.CompareTo(rectangles[b].X));

            var componentRects = component.Select(i => rectangles[i]).ToList();
            var componentTexts = component.Select(i => texts[i]).ToList();

            var lineBounds = MergeRectangles(componentRects);
            var lineText = string.Join(" ", componentTexts);

            lines.Add((lineText, lineBounds, component));
        }

        return lines;
    }

    private static OcrResult ConvertToOcrResults(
        List<TextBoundary> wordTextBoundaries,
        List<string> wordTexts,
        List<double> wordConfidences,
        List<(string text, Rectangle bounds, List<int> wordIndices)> lines,
        Image<Rgb24> image)
    {

        var result = new OcrResult
        {
            PageNumber = -1, // Updated to real value later
            Blocks = [], // TODO
            Lines = [],
            Words = []
        };

        // Create Word objects
        for (int i = 0; i < wordTextBoundaries.Count; i++)
        {
            result.Words.Add(new Word
            {
                Id = $"word_{i}",
                BoundingBox = CreateBoundingBox(wordTextBoundaries[i], image.Width, image.Height),
                Confidence = Math.Round(wordConfidences[i], 6),
                Text = wordTexts[i]
            });
        }

        // Create Line objects
        for (int i = 0; i < lines.Count; i++)
        {
            var (text, bounds, wordIndices) = lines[i];

            // Calculate line confidence as geometric mean of word confidences
            var lineWordConfidences = wordIndices.Select(idx => wordConfidences[idx]).ToList();
            var lineConfidence = lineWordConfidences.Count > 0
                ? Math.Pow(lineWordConfidences.Aggregate(1.0, (a, b) => a * b), 1.0 / lineWordConfidences.Count)
                : 0.0;

            result.Lines.Add(new Line
            {
                Id = $"line_{i}",
                BoundingBox = CreateBoundingBox(bounds, image.Width, image.Height),
                Confidence = Math.Round(lineConfidence, 6),
                Text = text,
                WordIds = wordIndices.Select(idx => $"word_{idx}").ToList()
            });
        }

        return result;
    }

    private static bool CanMerge(Rectangle rect1, Rectangle rect2)
    {
        // Calculate IoU (Intersection over Union)
        var intersectLeft = Math.Max(rect1.Left, rect2.Left);
        var intersectTop = Math.Max(rect1.Top, rect2.Top);
        var intersectRight = Math.Min(rect1.Right, rect2.Right);
        var intersectBottom = Math.Min(rect1.Bottom, rect2.Bottom);

        var intersectWidth = Math.Max(0, intersectRight - intersectLeft);
        var intersectHeight = Math.Max(0, intersectBottom - intersectTop);
        var intersectArea = intersectWidth * intersectHeight;

        var area1 = rect1.Width * rect1.Height;
        var area2 = rect2.Width * rect2.Height;
        var unionArea = area1 + area2 - intersectArea;

        var iou = unionArea > 0 ? (double)intersectArea / unionArea : 0;

        // If significant overlap, merge them
        if (iou >= 0.5)
            return true;

        // Otherwise, check if they're adjacent words on the same line
        // Determine left/right by horizontal center
        var center1 = rect1.X + rect1.Width / 2.0;
        var center2 = rect2.X + rect2.Width / 2.0;

        var leftRect = center1 <= center2 ? rect1 : rect2;
        var rightRect = center1 <= center2 ? rect2 : rect1;

        var leftTopRight = new Point(leftRect.Right, leftRect.Top);
        var leftBottomRight = new Point(leftRect.Right, leftRect.Bottom);
        var rightTopLeft = new Point(rightRect.Left, rightRect.Top);
        var rightBottomLeft = new Point(rightRect.Left, rightRect.Bottom);

        var topDistance = AnisotropicDistance(leftTopRight, rightTopLeft);
        var bottomDistance = AnisotropicDistance(leftBottomRight, rightBottomLeft);

        // Text height is proportional to character height and width
        var maxDistance = leftRect.Height * 1.5;
        return topDistance <= maxDistance && bottomDistance <= maxDistance;
    }

    private static double AnisotropicDistance(Point p1, Point p2)
    {
        // Vertical gaps are amplified and horizontal gaps are reduced
        var horizontalWeight = 0.67;
        var verticalWeight = 2.0;

        var dx = Math.Abs(p1.X - p2.X);
        var dy = Math.Abs(p1.Y - p2.Y);

        return Math.Sqrt(horizontalWeight * dx * dx + verticalWeight * dy * dy);
    }

    private static void DFS(int node, List<List<int>> adjacency, bool[] visited, List<int> component)
    {
        visited[node] = true;
        component.Add(node);

        foreach (var neighbor in adjacency[node])
        {
            if (!visited[neighbor])
            {
                DFS(neighbor, adjacency, visited, component);
            }
        }
    }

    private static Rectangle MergeRectangles(List<Rectangle> rectangles)
    {
        var minX = rectangles.Min(r => r.Left);
        var minY = rectangles.Min(r => r.Top);
        var maxX = rectangles.Max(r => r.Right);
        var maxY = rectangles.Max(r => r.Bottom);

        return Rectangle.FromLTRB(minX, minY, maxX, maxY);
    }


    private static BoundingBox CreateBoundingBox(TextBoundary textBoundary, int imageWidth, int imageHeight)
    {
        var rect = textBoundary.AARectangle;

        // Create normalized coordinates (0-1 range) with 6 decimal digits
        double normalizedX = Math.Round((double)rect.X / imageWidth, 6);
        double normalizedY = Math.Round((double)rect.Y / imageHeight, 6);
        double normalizedWidth = Math.Round((double)rect.Width / imageWidth, 6);
        double normalizedHeight = Math.Round((double)rect.Height / imageHeight, 6);

        // Create polygon from TextBoundary (normalized)
        var polygon = textBoundary.Polygon.Select(p => new JsonPoint
        {
            X = Math.Round((double)p.X / imageWidth, 6),
            Y = Math.Round((double)p.Y / imageHeight, 6)
        }).ToList();

        // Create oriented rectangle from TextBoundary (normalized)
        var oRectangle = textBoundary.ORectangle.Select(p => new JsonPoint
        {
            X = Math.Round((double)p.X / imageWidth, 6),
            Y = Math.Round((double)p.Y / imageHeight, 6)
        }).ToList();

        // Create axis-aligned rectangle
        var aaRectangle = new AARectangle
        {
            X = normalizedX,
            Y = normalizedY,
            Width = normalizedWidth,
            Height = normalizedHeight
        };

        return new BoundingBox
        {
            Polygon = polygon,
            ORectangle = oRectangle,
            AARectangle = aaRectangle
        };
    }

    private static BoundingBox CreateBoundingBox(Rectangle rect, int imageWidth, int imageHeight)
    {
        // Create normalized coordinates (0-1 range) with 6 decimal digits
        double normalizedX = Math.Round((double)rect.X / imageWidth, 6);
        double normalizedY = Math.Round((double)rect.Y / imageHeight, 6);
        double normalizedWidth = Math.Round((double)rect.Width / imageWidth, 6);
        double normalizedHeight = Math.Round((double)rect.Height / imageHeight, 6);

        // Create 4-point polygon (clockwise from top-left)
        var polygon = new List<JsonPoint>
        {
            new() { X = normalizedX, Y = normalizedY }, // Top-left
            new() { X = Math.Round(normalizedX + normalizedWidth, 6), Y = normalizedY }, // Top-right
            new() { X = Math.Round(normalizedX + normalizedWidth, 6), Y = Math.Round(normalizedY + normalizedHeight, 6) }, // Bottom-right
            new() { X = normalizedX, Y = Math.Round(normalizedY + normalizedHeight, 6) } // Bottom-left
        };

        // For lines, oriented rectangle is same as polygon (axis-aligned)
        var oRectangle = new List<JsonPoint>(polygon);

        // Create axis-aligned rectangle
        var aaRectangle = new AARectangle
        {
            X = normalizedX,
            Y = normalizedY,
            Width = normalizedWidth,
            Height = normalizedHeight
        };

        return new BoundingBox
        {
            Polygon = polygon,
            ORectangle = oRectangle,
            AARectangle = aaRectangle
        };
    }
}
