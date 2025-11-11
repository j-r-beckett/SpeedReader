// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Ocr.Algorithms;
using Resources;

namespace Ocr.Test.Algorithms;

public class GreedyCTCTests
{
    [Fact]
    public void GreedyCTCDecode_EmptySequence_ReturnsEmptyStringAndZeroConfidence()
    {
        // Arrange
        var ctcSequence = Array.Empty<float>();

        // Act
        var (text, confidence) = ctcSequence.GreedyCTCDecode();

        // Assert
        Assert.Equal("", text);
        Assert.Equal(0.0, confidence);
    }

    [Fact]
    public void GreedyCTCDecode_SingleCharacterSingleTimestep_ReturnsCharacterWithConfidence()
    {
        // Arrange
        var numClasses = CharacterDictionary.Count;
        var ctcSequence = new float[numClasses];
        int charIndex = 1;
        ctcSequence[charIndex] = 0.9f;
        ctcSequence[CharacterDictionary.Blank] = 0.1f;

        // Act
        var (text, confidence) = ctcSequence.GreedyCTCDecode();

        // Assert
        Assert.Single(text);
        Assert.Equal(CharacterDictionary.IndexToChar(charIndex), text[0]);
        Assert.Equal(0.9, confidence, precision: 5);
    }

    [Fact]
    public void GreedyCTCDecode_AllBlankTokens_ReturnsEmptyString()
    {
        // Arrange
        var numClasses = CharacterDictionary.Count;
        var numTimesteps = 10;
        var ctcSequence = new float[numClasses * numTimesteps];
        for (int t = 0; t < numTimesteps; t++)
        {
            ctcSequence[t * numClasses + CharacterDictionary.Blank] = 1.0f;
        }

        // Act
        var (text, confidence) = ctcSequence.GreedyCTCDecode();

        // Assert
        Assert.Equal("", text);
        Assert.Equal(0.0, confidence);
    }

    [Fact]
    public void GreedyCTCDecode_SameCharacterConsecutive_AppearsOnce()
    {
        // Arrange
        var numClasses = CharacterDictionary.Count;
        var numTimesteps = 5;
        var ctcSequence = new float[numClasses * numTimesteps];
        int charIndex = 1;
        for (int t = 0; t < numTimesteps; t++)
        {
            ctcSequence[t * numClasses + charIndex] = 0.8f;
        }

        // Act
        var (text, confidence) = ctcSequence.GreedyCTCDecode();

        // Assert
        Assert.Single(text);
        Assert.Equal(CharacterDictionary.IndexToChar(charIndex), text[0]);
    }

    [Fact]
    public void GreedyCTCDecode_SameCharacterWithBlankBetween_AppearsTwice()
    {
        // Arrange
        var numClasses = CharacterDictionary.Count;
        var numTimesteps = 3;
        var ctcSequence = new float[numClasses * numTimesteps];
        int charIndex = 1;
        ctcSequence[0 * numClasses + charIndex] = 0.9f;
        ctcSequence[1 * numClasses + CharacterDictionary.Blank] = 0.9f;
        ctcSequence[2 * numClasses + charIndex] = 0.9f;

        // Act
        var (text, confidence) = ctcSequence.GreedyCTCDecode();

        // Assert
        Assert.Equal(2, text.Length);
        Assert.Equal(CharacterDictionary.IndexToChar(charIndex), text[0]);
        Assert.Equal(CharacterDictionary.IndexToChar(charIndex), text[1]);
    }

    [Fact]
    public void GreedyCTCDecode_MultipleCharacters_DecodesCorrectly()
    {
        // Arrange
        var numClasses = CharacterDictionary.Count;
        var numTimesteps = 3;
        var ctcSequence = new float[numClasses * numTimesteps];
        int char1 = 1;
        int char2 = 2;
        int char3 = 3;
        ctcSequence[0 * numClasses + char1] = 0.9f;
        ctcSequence[1 * numClasses + char2] = 0.8f;
        ctcSequence[2 * numClasses + char3] = 0.7f;

        // Act
        var (text, confidence) = ctcSequence.GreedyCTCDecode();

        // Assert
        Assert.Equal(3, text.Length);
        Assert.Equal(CharacterDictionary.IndexToChar(char1), text[0]);
        Assert.Equal(CharacterDictionary.IndexToChar(char2), text[1]);
        Assert.Equal(CharacterDictionary.IndexToChar(char3), text[2]);
    }

    [Fact]
    public void GreedyCTCDecode_CharacterWithVaryingProbabilities_UsesMaxProbability()
    {
        // Arrange
        var numClasses = CharacterDictionary.Count;
        var numTimesteps = 4;
        var ctcSequence = new float[numClasses * numTimesteps];
        int charIndex = 1;
        ctcSequence[0 * numClasses + charIndex] = 0.6f;
        ctcSequence[1 * numClasses + charIndex] = 0.9f;
        ctcSequence[2 * numClasses + charIndex] = 0.7f;
        ctcSequence[3 * numClasses + charIndex] = 0.5f;

        // Act
        var (text, confidence) = ctcSequence.GreedyCTCDecode();

        // Assert
        Assert.Single(text);
        Assert.Equal(0.9, confidence, precision: 5);
    }

    [Fact]
    public void GreedyCTCDecode_MultipleCharactersWithVaryingProbabilities_ComputesGeometricMean()
    {
        // Arrange
        var numClasses = CharacterDictionary.Count;
        var numTimesteps = 5;
        var ctcSequence = new float[numClasses * numTimesteps];
        int char1 = 1;
        int char2 = 2;
        ctcSequence[0 * numClasses + char1] = 0.6f;
        ctcSequence[1 * numClasses + char1] = 0.8f;
        ctcSequence[2 * numClasses + char2] = 0.5f;
        ctcSequence[3 * numClasses + char2] = 0.9f;
        ctcSequence[4 * numClasses + char2] = 0.7f;

        // Act
        var (text, confidence) = ctcSequence.GreedyCTCDecode();

        // Assert
        Assert.Equal(2, text.Length);
        Assert.Equal(Math.Sqrt(0.8 * 0.9), confidence, precision: 5);
    }

    [Fact]
    public void GreedyCTCDecode_BlanksInterspersed_IgnoresBlanksProperly()
    {
        // Arrange
        var numClasses = CharacterDictionary.Count;
        var numTimesteps = 7;
        var ctcSequence = new float[numClasses * numTimesteps];
        int char1 = 1;
        int char2 = 2;
        ctcSequence[0 * numClasses + CharacterDictionary.Blank] = 0.9f;
        ctcSequence[1 * numClasses + char1] = 0.8f;
        ctcSequence[2 * numClasses + char1] = 0.8f;
        ctcSequence[3 * numClasses + CharacterDictionary.Blank] = 0.9f;
        ctcSequence[4 * numClasses + char2] = 0.7f;
        ctcSequence[5 * numClasses + CharacterDictionary.Blank] = 0.9f;
        ctcSequence[6 * numClasses + CharacterDictionary.Blank] = 0.9f;

        // Act
        var (text, _) = ctcSequence.GreedyCTCDecode();

        // Assert
        Assert.Equal(2, text.Length);
        Assert.Equal(CharacterDictionary.IndexToChar(char1), text[0]);
        Assert.Equal(CharacterDictionary.IndexToChar(char2), text[1]);
    }

    [Fact]
    public void IndexOfMax_ReturnsFirstWhenEqual()
    {
        // Arrange
        float[] data = [4.0f, 5.0f, 5.0f, 5.0f, 5.0f];

        // Act
        var index = GreedyCTC.IndexOfMax(data.AsSpan());

        // Assert
        Assert.Equal(1, index);
    }

    [Fact]
    public void IndexOfMax_FuzzAgainstSimpleImplementation()
    {
        // Arrange
        var random = new Random(0);
        const int iterations = 100000;
        int[] sizes = [0, 1, 7, 8, 15, 16, 17, 31, 32, 33, 63, 64, 65, 100, 500, 1000, 5000];

        foreach (var size in sizes)
        {
            for (int i = 0; i < iterations / sizes.Length; i++)
            {
                var data = new float[size];
                for (int j = 0; j < size; j++)
                {
                    data[j] = (float)(random.NextDouble() * 200.0 - 100.0);
                }

                // Act
                var expected = SimpleIndexOfMax(data.AsSpan());
                var actual = GreedyCTC.IndexOfMax(data.AsSpan());

                // Assert
                Assert.Equal(expected, actual);
            }
        }
    }

    [Fact]
    public void IndexOfMax_PositiveInfinity_ReturnsCorrectIndex()
    {
        // Arrange
        float[] data = [1.0f, 2.0f, float.PositiveInfinity, 3.0f];

        // Act
        var index = GreedyCTC.IndexOfMax(data.AsSpan());

        // Assert
        Assert.Equal(2, index);
    }

    [Fact]
    public void IndexOfMax_NegativeInfinity_HandledCorrectly()
    {
        // Arrange
        float[] data = [float.NegativeInfinity, -5.0f, -10.0f];

        // Act
        var index = GreedyCTC.IndexOfMax(data.AsSpan());

        // Assert
        Assert.Equal(1, index);
    }

    private static int SimpleIndexOfMax(ReadOnlySpan<float> span)
    {
        if (span.IsEmpty)
            return -1;

        int maxIndex = 0;
        float maxValue = span[0];

        for (int i = 1; i < span.Length; i++)
        {
            if (span[i] > maxValue)
            {
                maxValue = span[i];
                maxIndex = i;
            }
        }

        return maxIndex;
    }
}
