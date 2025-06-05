using System.Numerics.Tensors;

namespace TextRecognition;

public class PostProcessor
{
    private readonly CharacterDictionary _dictionary;

    public PostProcessor(CharacterDictionary dictionary)
    {
        _dictionary = dictionary;
    }

    public string[] DecodeCTC(Tensor<float> probabilities)
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

    private string DecodeSingleSequence(Tensor<float> probabilities, int batchIndex, int seqLen, int numClasses)
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
            if (maxIndex != prevIndex && !_dictionary.IsBlankToken(maxIndex))
            {
                char character = _dictionary.IndexToChar(maxIndex);
                decoded.Add(character);
            }

            prevIndex = maxIndex;
        }

        return new string(decoded.ToArray());
    }
}