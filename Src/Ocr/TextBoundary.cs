using SixLabors.ImageSharp;

namespace Ocr
{
    public class TextBoundary
    {
        public Rectangle AARectangle { get; }
        public List<(int X, int Y)> ORectangle { get; }
        public List<(int X, int Y)> Polygon { get; }

        private TextBoundary(List<(int X, int Y)> polygon, Rectangle aaRectangle, List<(int X, int Y)> oRectangle)
        {
            Polygon = polygon;
            AARectangle = aaRectangle;
            ORectangle = oRectangle;
        }

        public static TextBoundary Create(List<(int X, int Y)> polygon)
        {
            if (polygon == null || polygon.Count == 0)
                throw new ArgumentException("Polygon cannot be null or empty", nameof(polygon));

            // Calculate axis-aligned rectangle using the same logic as DBNet.GetBoundingBox
            int maxX = int.MinValue;
            int maxY = int.MinValue;
            int minX = int.MaxValue;
            int minY = int.MaxValue;

            foreach ((int x, int y) in polygon)
            {
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
            }

            // Create axis-aligned rectangle
            var aaRectangle = new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);

            // For now, set oriented rectangle to the corners of the axis-aligned rectangle
            var oRectangle = new List<(int X, int Y)>
            {
                (minX, minY),           // Top-left
                (maxX, minY),           // Top-right
                (maxX, maxY),           // Bottom-right
                (minX, maxY)            // Bottom-left
            };

            return new TextBoundary(polygon, aaRectangle, oRectangle);
        }
    }
}
