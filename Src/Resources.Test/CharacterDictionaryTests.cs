namespace Resources.Test;

public class CharacterDictionaryTests
{
    [Theory]
    [InlineData(4544, 'a')]  // Line 4544 in data file -> index 4544 (4543 + 1 for blank offset)
    [InlineData(4136, 'Z')]  // Line 4136 in data file -> index 4136 (4135 + 1 for blank offset)
    [InlineData(27, '8')]    // Line 27 in data file -> index 27 (26 + 1 for blank offset)
    [InlineData(166, '!')]   // Line 166 in data file -> index 166 (165 + 1 for blank offset)
    [InlineData(0, '\0')]    // Blank token at index 0
    [InlineData(6624, ' ')]  // Space character at index 6624
    public void IndexToChar_ReturnsExpectedCharacter(int index, char expectedChar)
    {
        var actualChar = CharacterDictionary.IndexToChar(index);
        Assert.Equal(expectedChar, actualChar);
    }

    [Fact]
    public void Count_ReturnsExpectedVocabularySize()
    {
        Assert.Equal(6625, CharacterDictionary.Count);
    }

    [Fact]
    public void IndexToChar_UnknownIndex_ReturnsQuestionMark()
    {
        var actualChar = CharacterDictionary.IndexToChar(99999);
        Assert.Equal('?', actualChar);
    }
}