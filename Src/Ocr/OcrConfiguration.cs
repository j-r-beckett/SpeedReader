namespace Ocr;

public class OcrConfiguration
{
    public DbNetConfiguration DbNet { get; set; } = new();
    public SvtrConfiguration Svtr { get; set; } = new();
    public bool CacheFirstInference { get; set; } = false;
}

public class DbNetConfiguration
{
    public int Width { get; set; } = 640;
    public int Height { get; set; } = 640;
}

public class SvtrConfiguration
{
    public int Width { get; set; } = 160;
    public int Height { get; set; } = 48;
}
