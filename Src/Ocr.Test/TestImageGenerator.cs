using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Fonts;

namespace Ocr.Test;

public record TextBox(Rectangle Bounds);

public record GeneratedImage(
    Image<Rgb24> Image,
    string[] RenderedTexts,
    Rectangle[] TextBoxes
);

public static class TestImageGenerator
{
    public static Image<Rgb24> Generate(Size imageSize, params TextBox[] textBoxes)
    {
        var image = new Image<Rgb24>(imageSize.Width, imageSize.Height, Color.White);
        
        // Get a font
        FontFamily fontFamily;
        if (!SystemFonts.TryGet("Arial", out fontFamily))
        {
            fontFamily = SystemFonts.Families.First();
        }
        var font = fontFamily.CreateFont(48, FontStyle.Regular);
        
        // Draw each box and its text
        foreach (var textBox in textBoxes)
        {
            var box = textBox.Bounds;
            
            // Draw the bounding box
            image.Mutate(ctx => ctx.Draw(Pens.Solid(Color.Gray, 2), new RectangleF(box.X, box.Y, box.Width, box.Height)));
            
            // Draw "TEST" in the box
            image.Mutate(ctx => ctx.DrawText("TEST", font, Color.Black, new PointF(box.X, box.Y)));
        }
        
        return image;
    }
}