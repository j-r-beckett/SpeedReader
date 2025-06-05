using System.Reflection;

namespace TextRecognition;

public class CharacterDictionary
{
    private readonly Dictionary<int, char> _indexToChar;
    private readonly Dictionary<char, int> _charToIndex;

    public CharacterDictionary()
    {
        _indexToChar = new Dictionary<int, char>();
        _charToIndex = new Dictionary<char, int>();

        LoadFromFile();
    }

    private void LoadFromFile()
    {
        // Load character dictionary from TextRecognition project directory
        var assemblyLocation = Assembly.GetExecutingAssembly().Location;
        var assemblyDir = Path.GetDirectoryName(assemblyLocation)!;
        var dictPath = Path.Combine(assemblyDir, "svtrv2_character_dict.txt");

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
            _charToIndex[character] = index;
        }

        // Index 6624: Space character (completing the 6625 vocabulary)
        _indexToChar[6624] = ' ';
        _charToIndex[' '] = 6624;
    }

    public char IndexToChar(int index)
    {
        return _indexToChar.TryGetValue(index, out char c) ? c : '?';
    }

    public int CharToIndex(char c)
    {
        return _charToIndex.TryGetValue(c, out int index) ? index : 0; // Return blank token if not found
    }

    public int VocabularySize => 6625; // 6623 characters + blank token + space

    public bool IsBlankToken(int index) => index == 0;

    public int BlankTokenIndex => 0;
}