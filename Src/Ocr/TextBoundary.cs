using Ocr.Algorithms;
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

            // Use new algorithms for rectangle computation
            var aaRectangle = BoundingRectangles.ComputeAxisAlignedRectangle(polygon);
            var oRectangle = BoundingRectangles.ComputeOrientedRectangle(polygon);

            return new TextBoundary(polygon, aaRectangle, oRectangle);
        }
    }
}
