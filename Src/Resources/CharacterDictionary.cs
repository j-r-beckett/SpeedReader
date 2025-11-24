// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Text;

namespace Resources;

public class CharacterDictionary
{
    public const int Blank = 0;

    private readonly Dictionary<int, char> _indexToChar;

    public CharacterDictionary()
    {
        _indexToChar = new Dictionary<int, char>();

        var data = new Resource("CharacterDictionary.Data.txt").Bytes;
        var content = Encoding.UTF8.GetString(data);
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Index 0 reserved for CTC blank token
        _indexToChar[0] = '\0'; // Use null character for blank

        // Load characters from file (indices 1-6623)
        for (int i = 0; i < lines.Length; i++)
        {
            if (string.IsNullOrEmpty(lines[i]))
            {
                continue;
            }

            char character = lines[i][0]; // Take first character of each line
            int index = i + 1; // Offset by 1 for blank token

            _indexToChar[index] = character;
        }

        // Index 6624: Space character (completing the 6625 vocabulary)
        _indexToChar[6624] = ' ';
    }

    public char IndexToChar(int index) => _indexToChar.GetValueOrDefault(index, '?');

    public int Count => _indexToChar.Count;
}
