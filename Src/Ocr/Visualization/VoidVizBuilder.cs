using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ocr.Visualization;

public class VoidVizBuilder : VizBuilder
{
    public VoidVizBuilder(Image<Rgb24> sourceImage) : base(sourceImage)
    {
    }

    public override void AddDetectionResults(List<Rectangle> rectangles, Buffer<float>? probabilityMap = null)
    {
        // No-op
    }

    public override void AddRecognitionResults(List<Rectangle> rectangles, List<string> texts)
    {
        // No-op
    }

    public override void AddMergedResults(List<Rectangle> mergedRectangles, List<string> mergedTexts)
    {
        // No-op
    }

    public override Image<Rgb24> Build()
    {
        throw new InvalidOperationException("VoidVizBuilder.Build() should never be called. Visualization mode is set to None.");
    }
}
