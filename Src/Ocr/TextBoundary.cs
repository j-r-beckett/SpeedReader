using SixLabors.ImageSharp;

namespace Ocr
{
    public class TextBoundary
    {
        public Rectangle AARectangle { get; }
        public List<(int X, int Y)> ORectangle { get; }
        public List<(int X, int Y)> Polygon { get; }

        public TextBoundary(List<(int X, int Y)> polygon, Rectangle aaRectangle, List<(int X, int Y)> oRectangle)
        {
            Polygon = polygon;
            AARectangle = aaRectangle;
            ORectangle = oRectangle;
        }
    }
}
