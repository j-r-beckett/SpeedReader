using System.Numerics.Tensors;
using System.Text;

namespace Ocr.Algorithms;

public static class CTC
{
    public static string DecodeSingleSequence(Tensor<float> sequence)
    {
        var (text, _) = DecodeSingleSequenceWithConfidence(sequence);
        return text;
    }

    public static (string text, double confidence) DecodeSingleSequenceWithConfidence(Tensor<float> sequence)
    {
        var decoded = new StringBuilder();
        var characterConfidences = new List<double>();

        int prevIndex = -1;
        double currentCharMaxProb = 0.0;
        int currentCharIndex = -1;

        int numSteps = Convert.ToInt32(sequence.Lengths[0]);
        for (int step = 0; step < numSteps; step++)
        {
            var probabilities = sequence[[step..(step + 1), Range.All]];
            int maxIndex = Convert.ToInt32(Tensor.IndexOfMax<float>(probabilities));
            double maxProb = probabilities[0, maxIndex];

            // CTC greedy decoding rule: only add if different from previous and not blank
            if (maxIndex != prevIndex && maxIndex != CharacterDictionary.Blank)
            {
                // If we had a previous character, save its confidence
                if (prevIndex != -1 && prevIndex != CharacterDictionary.Blank)
                {
                    characterConfidences.Add(currentCharMaxProb);
                }

                // Start new character
                char character = CharacterDictionary.IndexToChar(maxIndex);
                decoded.Append(character);
                currentCharMaxProb = maxProb; // Reset for new character
                currentCharIndex = maxIndex;
            }
            else if (maxIndex == currentCharIndex && currentCharIndex != -1)
            {
                // Same character as current, update max probability for this character
                currentCharMaxProb = Math.Max(currentCharMaxProb, maxProb);
            }

            prevIndex = maxIndex;
        }

        // Don't forget the last character's confidence
        if (decoded.Length > 0)
        {
            characterConfidences.Add(currentCharMaxProb);
        }

        // Calculate geometric mean for sequence-level confidence
        double geometricMean = characterConfidences.Count > 0
            ? Math.Pow(characterConfidences.Aggregate(1.0, (a, b) => a * b), 1.0 / characterConfidences.Count)
            : 0.0;

        return (decoded.ToString(), geometricMean);
    }
}
