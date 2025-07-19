using System.Numerics.Tensors;
using Ocr.Algorithms;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Ocr;

public class SVTRv2
{
    private readonly int _width;
    private readonly int _height;

    public SVTRv2(SvtrConfiguration config)
    {
        _width = config.Width;
        _height = config.Height;
    }

    public int Width => _width;
    public int Height => _height;

    public float[] PreProcess(Image<Rgb24> image, TextBoundary textBoundary)
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


    public (string text, double confidence) PostProcess(float[] modelOutput)
    {
        return CTC.DecodeSingleSequence(modelOutput, CharacterDictionary.Count);
    }
}
