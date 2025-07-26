using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ocr.Visualization;

public class VoidVizBuilder : VizBuilder
{
    public VoidVizBuilder(Image<Rgb24> sourceImage) : base(sourceImage)
    {
    }

    public override Image<Rgb24> Render()
    {
        throw new InvalidOperationException("VoidVizBuilder does not support rendering");
    }
}
