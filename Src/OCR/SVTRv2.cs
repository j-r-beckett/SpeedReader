using System.Numerics.Tensors;
using System.Buffers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using OCR.Algorithms;

namespace OCR;

public static class SVTRv2
{
    private const int TargetHeight = 48;
    private const int MinWidth = 12;
    private const int MaxWidth = 320;

    public static Tensor<float> PreProcess(Image<Rgb24>[] batch)
    {
        if (batch.Length == 0)
        {
            throw new ArgumentException("Batch cannot be empty", nameof(batch));
        }

        int maxWidth = CalculateMaxWidth(batch);

        // Create NCHW tensor for the batch
        ReadOnlySpan<nint> shape = [(nint)batch.Length, 3, TargetHeight, (nint)maxWidth];
        var data = new float[batch.Length * 3 * TargetHeight * maxWidth];
        var tensor = Tensor.Create(data, shape);
        var tensorSpan = tensor.AsTensorSpan();

        // Create temporary pixel buffer for each image using ArrayPool
        var pixelBuffer = ArrayPool<Rgb24>.Shared.Rent(maxWidth * TargetHeight);
        try
        {
            var memory = pixelBuffer.AsMemory(0, maxWidth * TargetHeight);

            for (int i = 0; i < batch.Length; i++)
            {
                Resize.ScaleResizeInto(batch[i], memory, maxWidth, TargetHeight, MinWidth, MaxWidth);

                // Convert single image from HWC to CHW layout in tensor
                TensorConversion.ConvertImageToNCHW(memory, tensorSpan, i, maxWidth, TargetHeight);
            }
        }
        finally
        {
            ArrayPool<Rgb24>.Shared.Return(pixelBuffer);
        }

        // Apply SVTRv2 normalization: [0,255] → [-1,1]
        // Optimized: (pixel/255 - 0.5) / 0.5 = pixel/127.5 - 1.0
        Tensor.Divide(tensor, 127.5f, tensor);
        Tensor.Subtract(tensor, 1.0f, tensor);

        return tensor;
    }

    public static string[] PostProcess(Tensor<float> probabilities)
    {
        // Input: [batch_size, sequence_length, num_classes]
        // sequence_length = spatial positions from left-to-right across text image
        // num_classes = vocabulary size (6625 for SVTRv2)
        // Output: Decoded text strings

        var results = new List<string>();
        var batchSize = (int)probabilities.Lengths[0];
        var sequenceLength = (int)probabilities.Lengths[1];
        var numClasses = (int)probabilities.Lengths[2];

        for (int batch = 0; batch < batchSize; batch++)
        {
            string text = DecodeSingleSequence(probabilities, batch, sequenceLength, numClasses);
            results.Add(text);
        }

        return results.ToArray();
    }

    internal static int CalculateMaxWidth(Image<Rgb24>[] batch)
    {
        int maxWidth = MinWidth;

        foreach (var image in batch)
        {
            // Calculate target width maintaining aspect ratio with fixed height of 48px
            double aspectRatio = (double)image.Width / image.Height;
            int targetWidth = (int)Math.Round(aspectRatio * TargetHeight);
            
            // Clamp to reasonable bounds
            targetWidth = Math.Max(MinWidth, Math.Min(MaxWidth, targetWidth));
            maxWidth = Math.Max(maxWidth, targetWidth);
        }

        return maxWidth;
    }


    private static string DecodeSingleSequence(Tensor<float> probabilities, int batchIndex, int seqLen, int numClasses)
    {
        var decoded = new List<char>();
        int prevIndex = -1;
        var probabilitySpan = probabilities.AsTensorSpan();

        for (int t = 0; t < seqLen; t++)
        {
            // Find argmax at spatial position t (left-to-right across text image)
            int maxIndex = 0;
            float maxValue = float.MinValue;

            for (int c = 0; c < numClasses; c++)
            {
                ReadOnlySpan<nint> indices = [batchIndex, t, c];
                float value = probabilitySpan[indices];
                if (value > maxValue)
                {
                    maxValue = value;
                    maxIndex = c;
                }
            }

            // CTC greedy decoding rule: only add if different from previous and not blank
            if (maxIndex != prevIndex && maxIndex != CharacterDictionary.Blank)
            {
                char character = CharacterDictionary.IndexToChar(maxIndex);
                decoded.Add(character);
            }

            prevIndex = maxIndex;
        }

        return new string(decoded.ToArray());
    }
}