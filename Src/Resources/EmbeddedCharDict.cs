// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Text;

namespace Resources;

public class EmbeddedCharDict
{
    public const int Blank = 0;

    private readonly char[] _indexToChar;

    public EmbeddedCharDict()
    {
        var data = new Resource("CharacterDictionary.Data.txt").Bytes;
        var content = Encoding.UTF8.GetString(data);
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Two additional characters are CTC blank (at [0]) and space (at [end])
        _indexToChar = new char[lines.Length + 2];
        _indexToChar[Blank] = '\0';  // CTC blank represented by the null character
        _indexToChar[^1] = ' ';

        // Load characters from dictionary file
        for (var i = 0; i < lines.Length; i++)
            _indexToChar[i + 1] = lines[i][0];
    }

    // Returns ? if index is out of range
    public char IndexToChar(int index) => index < 0 || index >= _indexToChar.Length ? '?' : _indexToChar[index];

    public int Count => _indexToChar.Length;
}
