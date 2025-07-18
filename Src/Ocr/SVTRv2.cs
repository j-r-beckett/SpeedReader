using System.Numerics.Tensors;
using Ocr.Algorithms;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Ocr;

public static class SVTRv2
{
    private const int ModelHeight = 48;
    private const int ModelWidth = 320;

    public static float[] PreProcess(Image<Rgb24> image, TextBoundary textBoundary)
    {
        float[] data = new float[ModelHeight * ModelWidth * 3];

        using var croppedImage = image.Clone(x => x.Crop(textBoundary.AARectangle));

        Resampling.AspectResizeInto(croppedImage, data, ModelWidth, ModelHeight, 127.5f);

        // Convert to CHW format in place
        TensorOps.NhwcToNchw(data, [ModelHeight, ModelWidth, 3]);

        // Apply SVTRv2 normalization: [0,255] -> [-1,1] for each channel
        for (int channel = 0; channel < 3; channel++)
        {
            int channelOffset = channel * ModelHeight * ModelWidth;
            var channelTensor = Tensor.Create(data, channelOffset, [ModelHeight, ModelWidth], default);

            Tensor.Divide(channelTensor, 127.5f, channelTensor);
            Tensor.Subtract(channelTensor, 1.0f, channelTensor);
        }

        return data;
    }


    public static (string text, double confidence) PostProcess(float[] modelOutput)
    {
        return CTC.DecodeSingleSequence(modelOutput, CharacterDictionary.Count);
    }
}
