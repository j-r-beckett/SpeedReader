using CommunityToolkit.HighPerformance;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ocr.Visualization;

public abstract class VizBuilder
{
    protected Image<Rgb24> _sourceImage;

    protected VizBuilder(Image<Rgb24> sourceImage)
    {
        _sourceImage = sourceImage;
    }

    public virtual void AddRectangles(List<Rectangle> rectangles) { }

    public virtual void AddProbabilityMap(Span2D<float> probabilityMap) { }

    public virtual void AddRecognitionResult(string text, TextBoundary boundary) { }

    public virtual void AddMergedResults(List<Rectangle> mergedRectangles, List<string> mergedTexts) { }

    public virtual void AddPolygons(List<List<(int X, int Y)>> polygons) { }

    public abstract Image<Rgb24> Render();

    public static VizBuilder Create(VizMode mode, Image<Rgb24> sourceImage)
    {
        return mode switch
        {
            VizMode.None => new VoidVizBuilder(sourceImage),
            VizMode.Basic => new BasicVizBuilder(sourceImage),
            VizMode.Diagnostic => new DiagnosticVizBuilder(sourceImage),
            _ => throw new ArgumentException($"Unknown visualization mode: {mode}")
        };
    }
}
