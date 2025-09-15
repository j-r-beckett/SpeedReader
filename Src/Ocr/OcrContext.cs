using Ocr.Visualization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ocr;

/// <summary>
/// Context object that carries the original image and visualization builder through the OCR pipeline.
/// Use this when both the original image and visualization are needed. For pipeline stages that only
/// need one or the other, you can pass them individually.
/// </summary>
public class OcrContext
{
    public Image<Rgb24> OriginalImage { get; }
    public VizBuilder VizBuilder { get; }

    public OcrContext(Image<Rgb24> originalImage, VizBuilder vizBuilder)
    {
        OriginalImage = originalImage;
        VizBuilder = vizBuilder;
    }
}
