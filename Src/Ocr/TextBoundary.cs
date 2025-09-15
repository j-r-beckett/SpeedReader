// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Ocr.Algorithms;
using SixLabors.ImageSharp;

namespace Ocr
{
    public class TextBoundary
    {
        public Rectangle AARectangle { get; }
        public List<(double X, double Y)> ORectangle { get; }
        public List<(int X, int Y)> Polygon { get; }

        private TextBoundary(List<(int X, int Y)> polygon, Rectangle aaRectangle, List<(double X, double Y)> oRectangle)
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
