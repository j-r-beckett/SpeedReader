using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Fonts;

namespace Ocr.Test;

public record TextBox(Rectangle Bounds);
public record RenderedText(string Text, Rectangle ActualBounds);
public record GeneratedImage(Image<Rgb24> Image, RenderedText[] RenderedTexts);

public static class TestImageGenerator
{
    private const float FontSizeFactor = 0.8f;
    private const int TextMargin = 5;
    
    private static readonly string[] BaseWords = 
    {
        "A", "GO", "CAT", "JUMP", "QUICK", "FROZEN", "RAINBOW", "SUNSHINE",
        "WONDERFUL", "BASKETBALL", "COMFORTABLE", "STRAWBERRIES", 
        "EXTRAORDINARY", "TRANSFORMATION", "INTERNATIONALLY",
        "BASKETBALLCOURT", "STRAWBERRIESNICE", "RAINBOWSUNSHINE",
        "WONDERFULBASKETBALL", "COMFORTABLESUNSHINE", "EXTRAORDINARYJOURNEY",
        "TRANSFORMATIONPROCESS", "INTERNATIONALLYKNOWN", "BASKETBALLCOURTOUTSIDE",
        "STRAWBERRIESANDCREAMTEA"
    };
    
    public static GeneratedImage Generate(Size imageSize, params TextBox[] textBoxes)
    {
        var image = new Image<Rgb24>(imageSize.Width, imageSize.Height, Color.White);
        var renderedTexts = new RenderedText[textBoxes.Length];
        var fontFamily = GetFontFamily();
        var digitGenerator = new Random(0); // Fresh instance for deterministic output
        
        for (int i = 0; i < textBoxes.Length; i++)
        {
            renderedTexts[i] = RenderTextBox(image, textBoxes[i], fontFamily, digitGenerator.Next(0, 10));
        }
        
        return new GeneratedImage(image, renderedTexts);
    }
    
    private static RenderedText RenderTextBox(Image<Rgb24> image, TextBox textBox, FontFamily fontFamily, int digit)
    {
        var box = textBox.Bounds;
        var fontSize = box.Height * FontSizeFactor;
        var font = fontFamily.CreateFont(fontSize, FontStyle.Regular);
        
        // Select and measure text
        var text = SelectTextForBox(box, font, digit);
        var textSize = TextMeasurer.MeasureSize(text, new TextOptions(font));
        
        // Calculate centered position
        var x = box.X + (box.Width - textSize.Width) / 2;
        var y = box.Y + (box.Height - textSize.Height) / 2;
        var actualBounds = new Rectangle((int)x, (int)y, (int)textSize.Width, (int)textSize.Height);
        
        // Draw everything
        image.Mutate(ctx =>
        {
            ctx.Draw(Pens.Solid(Color.LightGray, 2), box);          // Hint box
            ctx.Draw(Pens.Solid(Color.Red, 2), actualBounds);       // Actual bounds
            ctx.DrawText(text, font, Color.Black, new PointF(x, y)); // Text
        });
        
        return new RenderedText(text, actualBounds);
    }
    
    private static FontFamily GetFontFamily()
    {
        return SystemFonts.TryGet("Arial", out var arial) ? arial : SystemFonts.Families.First();
    }
    
    private static string SelectTextForBox(Rectangle box, Font font, int digit)
    {
        var availableWidth = box.Width - (TextMargin * 2);
        var bestWord = BaseWords[0]; // Default to shortest
        
        foreach (var word in BaseWords)
        {
            var candidateText = $"{word}{digit}";
            var textWidth = TextMeasurer.MeasureSize(candidateText, new TextOptions(font)).Width;
            
            if (textWidth <= availableWidth && word.Length > bestWord.Length)
            {
                bestWord = word;
            }
        }
        
        return $"{bestWord}{digit}";
    }
}

public static class TextBoxLayouts
{
    public static TextBox[] CreateGrid(int rows, int cols, int boxWidth, int boxHeight, int margin = 20, int startX = 20, int startY = 20)
    {
        var boxes = new List<TextBox>();
        
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                var x = startX + col * (boxWidth + margin);
                var y = startY + row * (boxHeight + margin);
                boxes.Add(new TextBox(new Rectangle(x, y, boxWidth, boxHeight)));
            }
        }
        
        return boxes.ToArray();
    }
    
    public static Size CalculateImageSize(int rows, int cols, int boxWidth, int boxHeight, int margin = 20, int padding = 40)
    {
        var width = cols * boxWidth + (cols - 1) * margin + padding;
        var height = rows * boxHeight + (rows - 1) * margin + padding;
        return new Size(width, height);
    }
}