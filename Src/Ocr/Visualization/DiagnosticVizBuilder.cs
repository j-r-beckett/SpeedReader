using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ocr.Visualization;

public class DiagnosticVizBuilder : BasicVizBuilder
{
    public DiagnosticVizBuilder(Image<Rgb24> sourceImage) : base(sourceImage) { }

    public override void AddDetectionResults(List<Rectangle> rectangles, Buffer<float> probabilityMap)
    {
    }

    public override void AddRecognitionResults(List<Rectangle> rectangles, List<string> texts)
    {
    }

    public override Image<Rgb24> Render()
    {
        // TODO: Implement full diagnostic visualization
        // For now, just use the base implementation
        return base.Render();
    }
}
