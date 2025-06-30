using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ocr.Visualization;

public class BasicVizBuilder : VizBuilder
{
    protected List<Rectangle> _mergedRectangles = new();
    protected List<string> _mergedTexts = new();

    public BasicVizBuilder(Image<Rgb24> sourceImage) : base(sourceImage)
    {
    }

    public override void AddDetectionResults(List<Rectangle> rectangles, Buffer<float>? probabilityMap = null)
    {
        // Basic mode ignores raw detection results
    }

    public override void AddRecognitionResults(List<Rectangle> rectangles, List<string> texts)
    {
        // Basic mode ignores unmerged recognition results
    }

    public override void AddMergedResults(List<Rectangle> mergedRectangles, List<string> mergedTexts)
    {
        _mergedRectangles = mergedRectangles;
        _mergedTexts = mergedTexts;
    }

    public override Image<Rgb24> Build()
    {
        // TODO: Implement actual visualization
        // For now, just return a clone of the source image
        return _sourceImage.Clone();
    }
}
