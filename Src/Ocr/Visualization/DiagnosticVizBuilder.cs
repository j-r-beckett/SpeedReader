using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ocr.Visualization;

public class DiagnosticVizBuilder : BasicVizBuilder
{
    private List<Rectangle> _rawRectangles = new();
    private List<string> _rawTexts = new();
    private Buffer<float>? _probabilityMap;

    public DiagnosticVizBuilder(Image<Rgb24> sourceImage) : base(sourceImage)
    {
    }

    public override void AddDetectionResults(List<Rectangle> rectangles, Buffer<float>? probabilityMap = null)
    {
        _rawRectangles = rectangles;
        _probabilityMap = probabilityMap;
    }

    public override void AddRecognitionResults(List<Rectangle> rectangles, List<string> texts)
    {
        _rawRectangles = rectangles;
        _rawTexts = texts;
    }

    public override Image<Rgb24> Build()
    {
        // TODO: Implement full diagnostic visualization
        // For now, just use the base implementation
        return base.Build();
    }
}
