// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;
using System.Numerics;
using System.Numerics.Tensors;
using System.Text;
using Resources;

namespace Experimental.Algorithms;

public static class GreedyCTC
{
    public static (string Text, double Confidence) GreedyCTCDecode(this float[] ctcSequence)
    {
        var numClasses = CharacterDictionary.Count;
        Debug.Assert(ctcSequence.Length % numClasses == 0);

        var decoded = new StringBuilder();
        var characterConfidences = new List<double>();

        int prevIndex = -1;
        double currentCharMaxProb = 0.0;
        int currentCharIndex = -1;

        int numSteps = ctcSequence.Length / numClasses;
        for (int step = 0; step < numSteps; step++)
        {
            int offset = step * numClasses;
            var sequence = ctcSequence.AsSpan().Slice(offset, numClasses);
            var maxIndex = IndexOfMax(sequence);
            double maxProb = ctcSequence[offset + maxIndex];

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

        // Last character's confidence
        if (decoded.Length > 0)
            characterConfidences.Add(currentCharMaxProb);

        // Calculate geometric mean using log-space
        var geometricMean = characterConfidences.Count > 0
            ? Math.Exp(characterConfidences.Average(Math.Log))
            : 0.0;

        return (decoded.ToString(), geometricMean);
    }

    internal static int IndexOfMax(ReadOnlySpan<float> span)
    {
        if (span.IsEmpty)
            return -1;

        int maxIndex = 0;
        float maxValue = span[0];

        int vectorSize = Vector<float>.Count;
        var maxVec = new Vector<float>(float.MinValue);  // Contains the max value seen so far

        int i = 0;
        if (Vector.IsHardwareAccelerated && span.Length >= vectorSize)
        {
            for (; i <= span.Length - vectorSize; i += vectorSize)
            {
                var vec = new Vector<float>(span.Slice(i, vectorSize));

                // Check if this vector contains value greater than any value in the current max vector
                if (Vector.GreaterThanAny(vec, maxVec))
                {
                    maxIndex = i + VectorIndexOfMax(vec);
                    maxValue = span[maxIndex];
                    maxVec = new Vector<float>(maxValue);
                }
            }
        }

        // Handle any remaining values
        for (; i < span.Length; i++)
        {
            if (span[i] > maxValue)
            {
                maxValue = span[i];
                maxIndex = i;
            }
        }

        return maxIndex;
    }

    private static int VectorIndexOfMax(Vector<float> vec)
    {
        int maxIndex = 0;
        for (int i = 1; i < Vector<float>.Count; i++)
        {
            if (vec[i] > vec[maxIndex])
                maxIndex = i;
        }
        return maxIndex;
    }

    // private static int IndexOfMax(Span<float> sequence)
    // {
    //     var maxIndex = 0;
    //     for (var i = 1; i < sequence.Length; i++)
    //     {
    //         if (sequence[i] > sequence[maxIndex])
    //             maxIndex = i;
    //     }
    //
    //     return maxIndex;
    // }
}
