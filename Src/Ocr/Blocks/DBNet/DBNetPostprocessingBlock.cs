using System.Threading.Tasks.Dataflow;
using CommunityToolkit.HighPerformance;
using Ocr.Algorithms;
using SixLabors.ImageSharp;

namespace Ocr.Blocks.DBNet;

public class DBNetPostprocessingBlock
{
    private readonly int _width;
    private readonly int _height;

    public IPropagatorBlock<(float[], OcrContext), (List<TextBoundary>, OcrContext)> Target { get; }

    public DBNetPostprocessingBlock(DbNetConfiguration config)
    {
        _width = config.Width;
        _height = config.Height;

        Target = new TransformBlock<(float[] RawResult, OcrContext Context), (List<TextBoundary>, OcrContext)>(input =>
        {
            // Add raw probability map to visualization BEFORE binarization
            input.Context.VizBuilder.AddProbabilityMap(input.RawResult.AsSpan().AsSpan2D(_height, _width));

            var textBoundaries = PostProcess(input.RawResult, input.Context.OriginalImage.Width, input.Context.OriginalImage.Height);

            input.Context.VizBuilder.AddRectangles(textBoundaries.Select(tb => tb.AARectangle).ToList());
            input.Context.VizBuilder.AddPolygons(textBoundaries.Select(tb => tb.Polygon).ToList());

            return (textBoundaries, input.Context);
        }, new ExecutionDataflowBlockOptions
        {
            BoundedCapacity = 1,
            MaxDegreeOfParallelism = 1
        });
    }

    private List<TextBoundary> PostProcess(float[] processedImage, int originalWidth, int originalHeight)
    {
        Thresholding.BinarizeInPlace(processedImage, 0.2f);
        var probabilityMapSpan = processedImage.AsSpan().AsSpan2D(_height, _width);
        var boundaries = BoundaryTracing.FindBoundaries(probabilityMapSpan);
        List<TextBoundary> textBoundaries = [];

        foreach (var boundary in boundaries)
        {
            // Simplify
            var simplifiedPolygon = PolygonSimplification.DouglasPeucker(boundary);

            // Dilate
            var dilatedPolygon = Dilation.DilatePolygon(simplifiedPolygon.ToList());

            // Convert back to original coordinate system
            double scale = Math.Max((double)originalWidth / probabilityMapSpan.Width, (double)originalHeight / probabilityMapSpan.Height);
            Scale(dilatedPolygon, scale);

            // Clamp coordinates to image bounds
            ClampToImageBounds(dilatedPolygon, originalWidth, originalHeight);

            textBoundaries.Add(TextBoundary.Create(dilatedPolygon));
        }

        return textBoundaries;
    }

    private static void Scale(List<(int X, int Y)> polygon, double scale)
    {
        for (int i = 0; i < polygon.Count; i++)
        {
            int originalX = (int)Math.Round(polygon[i].X * scale);
            int originalY = (int)Math.Round(polygon[i].Y * scale);
            polygon[i] = (originalX, originalY);
        }
    }

    private static void ClampToImageBounds(List<(int X, int Y)> polygon, int imageWidth, int imageHeight)
    {
        for (int i = 0; i < polygon.Count; i++)
        {
            int clampedX = Math.Max(0, Math.Min(imageWidth - 1, polygon[i].X));
            int clampedY = Math.Max(0, Math.Min(imageHeight - 1, polygon[i].Y));
            polygon[i] = (clampedX, clampedY);
        }
    }
}
