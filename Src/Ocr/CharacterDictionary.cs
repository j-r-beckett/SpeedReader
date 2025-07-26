using System.Reflection;

namespace Ocr;

public static class CharacterDictionary
{
    public const int Blank = 0;

    private static readonly Dictionary<int, char> _indexToChar;

    static CharacterDictionary()
    {
        // Load character dictionary from embedded resource
        _indexToChar = new Dictionary<int, char>();
        var assembly = Assembly.GetExecutingAssembly();

        using var stream = assembly.GetManifestResourceStream("Ocr.CharacterDictionary.Data.txt");
        if (stream == null)
            throw new FileNotFoundException("Embedded resource 'Ocr.CharacterDictionary.Data.txt' not found");

        using var reader = new StreamReader(stream);
        var lines = reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);

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

    public static char IndexToChar(int index) => _indexToChar.GetValueOrDefault(index, '?');

    public static int Count => _indexToChar.Count;
}
