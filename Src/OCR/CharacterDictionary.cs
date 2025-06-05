using System.Reflection;

namespace OCR;

public static class CharacterDictionary
{
    public const int Blank = 0;

    private static readonly Dictionary<int, char> _indexToChar;

    static CharacterDictionary()
    {
        // Load character dictionary from TextRecognition project directory
        _indexToChar = new Dictionary<int, char>();
        var assemblyLocation = Assembly.GetExecutingAssembly().Location;
        var assemblyDir = Path.GetDirectoryName(assemblyLocation)!;
        var dictPath = Path.Combine(assemblyDir, "CharacterDictionary.Data.txt");

        if (!File.Exists(dictPath))
        {
            throw new FileNotFoundException($"Character dictionary not found at: {dictPath}");
        }

        var lines = File.ReadAllLines(dictPath);

        // Index 0 reserved for CTC blank token
        _indexToChar[0] = '\0'; // Use null character for blank

        // Load characters from file (indices 1-6623)
        for (int i = 0; i < lines.Length; i++)
        {
            if (string.IsNullOrEmpty(lines[i])) continue;

            char character = lines[i][0]; // Take first character of each line
            int index = i + 1; // Offset by 1 for blank token

            _indexToChar[index] = character;
        }

        // Index 6624: Space character (completing the 6625 vocabulary)
        _indexToChar[6624] = ' ';
    }

    public static char IndexToChar(int index)
    {
        return _indexToChar.TryGetValue(index, out char c) ? c : '?';
    }
}
