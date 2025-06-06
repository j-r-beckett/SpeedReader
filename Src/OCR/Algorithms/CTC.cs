using System;
using System.Collections.Generic;

namespace OCR.Algorithms;

public static class CTC
{
    /// <summary>
    /// Performs CTC (Connectionist Temporal Classification) greedy decoding on a single sequence.
    /// Takes a span representing [sequence_length, num_classes] probabilities and returns decoded text.
    /// </summary>
    /// <param name="sequenceSpan">Span containing probabilities for one sequence in [seq_len, num_classes] format</param>
    /// <param name="seqLen">Length of the sequence (temporal dimension)</param>
    /// <param name="numClasses">Number of classes (vocabulary size)</param>
    /// <returns>Decoded text string using CTC greedy decoding rules</returns>
    public static string DecodeSingleSequence(Span<float> sequenceSpan, int seqLen, int numClasses)
    {
        var decoded = new List<char>();
        int prevIndex = -1;

        for (int t = 0; t < seqLen; t++)
        {
            // Find argmax at spatial position t (left-to-right across text image)
            int maxIndex = 0;
            float maxValue = float.MinValue;

            for (int c = 0; c < numClasses; c++)
            {
                // Calculate flat index for [t, c] in the sequence span
                int flatIndex = t * numClasses + c;
                float value = sequenceSpan[flatIndex];
                if (value > maxValue)
                {
                    maxValue = value;
                    maxIndex = c;
                }
            }

            // CTC greedy decoding rule: only add if different from previous and not blank
            // TODO: use beam search
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
