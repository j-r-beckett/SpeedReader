using System.Numerics.Tensors;
using System.Text;

namespace Ocr.Algorithms;

public static class CTC
{
    public static string DecodeSingleSequence(Tensor<float> sequence)
    {
        var decoded = new StringBuilder();
        int prevIndex = -1;

        int numSteps = Convert.ToInt32(sequence.Lengths[0]);
        for (int step = 0; step < numSteps; step++)
        {
            var probabilities = sequence[[step..(step + 1), Range.All]];
            int maxIndex = Convert.ToInt32(Tensor.IndexOfMax<float>(probabilities));

            // CTC greedy decoding rule: only add if different from previous and not blank
            if (maxIndex != prevIndex && maxIndex != CharacterDictionary.Blank)
            {
                char character = CharacterDictionary.IndexToChar(maxIndex);
                decoded.Append(character);
            }

            prevIndex = maxIndex;
        }

        return decoded.ToString();
    }
}
