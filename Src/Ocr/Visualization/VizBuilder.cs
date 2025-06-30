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

    public abstract void AddDetectionResults(List<Rectangle> rectangles, Buffer<float>? probabilityMap = null);

    public abstract void AddRecognitionResults(List<Rectangle> rectangles, List<string> texts);

    public abstract void AddMergedResults(List<Rectangle> mergedRectangles, List<string> mergedTexts);

    public abstract Image<Rgb24> Build();

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
