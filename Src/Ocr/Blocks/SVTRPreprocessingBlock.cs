using System.Numerics.Tensors;
using System.Threading.Tasks.Dataflow;
using Ocr.Algorithms;
using Ocr.Visualization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Ocr.Blocks;

public class SVTRPreprocessingBlock
{
    private readonly int _width;
    private readonly int _height;

    public IPropagatorBlock<(TextBoundary, Image<Rgb24>, VizBuilder), (float[], TextBoundary, Image<Rgb24>, VizBuilder)> Target { get; }

    public SVTRPreprocessingBlock(SvtrConfiguration config)
    {
        _width = config.Width;
        _height = config.Height;

        Target = new TransformBlock<(TextBoundary TextBoundary, Image<Rgb24> Image, VizBuilder VizBuilder), (float[], TextBoundary, Image<Rgb24>, VizBuilder)>(input
            => (PreProcess(input.Image, input.TextBoundary), input.TextBoundary, input.Image, input.VizBuilder));
    }

    private float[] PreProcess(Image<Rgb24> image, TextBoundary textBoundary)
    {
        float[] data = new float[_height * _width * 3];

        using var croppedImage = image.Clone(x => x.Crop(textBoundary.AARectangle));

        Resampling.AspectResizeInto(croppedImage, data, _width, _height, 127.5f);

        // Convert to CHW format in place
        TensorOps.NhwcToNchw(data, [_height, _width, 3]);

        // Apply SVTRv2 normalization: [0,255] -> [-1,1] for each channel
        for (int channel = 0; channel < 3; channel++)
        {
            int channelOffset = channel * _height * _width;
            var channelTensor = Tensor.Create(data, channelOffset, [_height, _width], default);

            Tensor.Divide(channelTensor, 127.5f, channelTensor);
            Tensor.Subtract(channelTensor, 1.0f, channelTensor);
        }

        return data;
    }
}
