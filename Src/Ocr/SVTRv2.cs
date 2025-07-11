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

    public static float[] PreProcess(Image<Rgb24> image, List<TextBoundary> textBoundaries)
    {
        int totalRectangles = textBoundaries.Count;
        int itemSize = ModelHeight * ModelWidth * 3;

        // Allocate array directly in HWC format (no batch dimension, just concatenated items)
        float[] data = new float[totalRectangles * itemSize];

        // Process each text boundary
        for (int i = 0; i < textBoundaries.Count; i++)
        {
            var rect = textBoundaries[i].AARectangle;

            // Crop the rectangle from the image
            using var croppedImage = image.Clone(x => x.Crop(rect));

            var dest = data.AsSpan().Slice(i * itemSize, itemSize);
            Resampling.AspectResizeInto(croppedImage, dest, ModelWidth, ModelHeight, 127.5f);

            // Convert this item to CHW format in place
            TensorOps.NhwcToNchw(dest, [ModelHeight, ModelWidth, 3]);

            // Apply SVTRv2 normalization: [0,255] → [-1,1] for each channel
            for (int channel = 0; channel < 3; channel++)
            {
                int channelOffset = i * itemSize + channel * ModelHeight * ModelWidth;
                var channelTensor = Tensor.Create(data, channelOffset, [ModelHeight, ModelWidth], default);

                Tensor.Divide(channelTensor, 127.5f, channelTensor);
                Tensor.Subtract(channelTensor, 1.0f, channelTensor);
            }
        }

        return data;  // Each item is now in CHW format
    }

    public static string[] PostProcess(float[] modelOutput, int numRectangles)
    {
        var (texts, _) = PostProcessWithConfidence(modelOutput, numRectangles);
        return texts;
    }

    public static (string[] texts, double[] confidences) PostProcessWithConfidence(float[] modelOutput, int numRectangles)
    {
        int numClasses = CharacterDictionary.Count;
        int sequenceLength = modelOutput.Length / numRectangles / numClasses;  // All CTC sequences are the same length

        var texts = new List<string>();
        var confidences = new List<double>();

        for (int i = 0; i < numRectangles; i++)
        {
            var region = Tensor.Create(modelOutput, i * sequenceLength * numClasses, [sequenceLength, numClasses], default);
            var (text, confidence) = CTC.DecodeSingleSequenceWithConfidence(region);
            texts.Add(text);
            confidences.Add(confidence);
        }

        return (texts.ToArray(), confidences.ToArray());
    }
}
