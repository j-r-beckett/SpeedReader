using System.Threading.Tasks.Dataflow;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Ocr.Blocks;

public static class OcrPostProcessingBlock
{
    public static IPropagatorBlock<(Image<Rgb24>, List<Rectangle>, List<string>), (Image<Rgb24>, List<Rectangle>, List<string>)> Create()
    {
        return new TransformBlock<(Image<Rgb24> Image, List<Rectangle> Rectangles, List<string> Texts), (Image<Rgb24>, List<Rectangle>, List<string>)>(data =>
        {
            var (filteredRectangles, filteredTexts) = RemoveEmptyText(data.Rectangles, data.Texts);
            
            return (data.Image, filteredRectangles, filteredTexts);
        });
    }

    private static (List<Rectangle>, List<string>) RemoveEmptyText(List<Rectangle> rectangles, List<string> texts)
    {
        var filteredRectangles = new List<Rectangle>();
        var filteredTexts = new List<string>();

        for (int i = 0; i < texts.Count; i++)
        {
            var trimmedText = texts[i].Trim();
            if (trimmedText != string.Empty)
            {
                filteredRectangles.Add(rectangles[i]);
                filteredTexts.Add(trimmedText);
            }
        }

        return (filteredRectangles, filteredTexts);
    }
}