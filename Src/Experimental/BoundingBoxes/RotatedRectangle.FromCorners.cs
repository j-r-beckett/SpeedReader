// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Diagnostics;

namespace Experimental.BoundingBoxes;

public static partial class RotatedRectangleExtensions
{
    public static RotatedRectangle ToRotatedRectangle(this List<PointF> corners)  // corners must be convex and in clockwise order
    {
        if (corners.Count != 4)
            throw new ArgumentException("Must have 4 corners");

        // Set 1 of parallel edges: corners[0] -> corners[1] and corners[2] -> corners[3]
        List<(PointF Start, PointF End)> parallelEdges1 = [(corners[0], corners[1]), (corners[2], corners[3])];

        // Set 2 of parallel edges: corners[1] -> corners[2] and corners[3] -> corners[0]
        List<(PointF Start, PointF End)> parallelEdges2 = [(corners[1], corners[2]), (corners[3], corners[0])];

        // Parallel edges should have the same length
        Debug.Assert(Math.Abs(EdgeLength(parallelEdges1[0]) - EdgeLength(parallelEdges1[1])) < 0.001);
        Debug.Assert(Math.Abs(EdgeLength(parallelEdges2[0]) - EdgeLength(parallelEdges2[1])) < 0.001);

        // Sort sets of parallel edges by edge length
        List<List<(PointF Start, PointF End)>> parallelEdgeSets = [parallelEdges1, parallelEdges2];
        parallelEdgeSets.Sort((e1, e2) => EdgeLength(e1[0]).CompareTo(EdgeLength(e2[0])));
        var (shortEdges, longEdges) = (parallelEdgeSets[0], parallelEdgeSets[1]);

        // The top edge is the long edge with the lowest Y value
        var topEdge = YMidpoint(longEdges[0]) < YMidpoint(longEdges[1]) ? longEdges[0] : longEdges[1];

        // Find the top left and top right points
        List<PointF> topEdgePoints = [topEdge.Start, topEdge.End];
        topEdgePoints.Sort((p1, p2) => p1.X.CompareTo(p2.X));
        var (topLeft, topRight) = (topEdgePoints[0], topEdgePoints[1]);

        // Calculate angle
        var angle = Math.Atan2(topRight.Y - topLeft.Y, topRight.X - topLeft.X);

        // Calculate height and width
        var height = EdgeLength(shortEdges[0]);
        var width = EdgeLength(longEdges[0]);

        return new RotatedRectangle
        {
            X = (int)Math.Round(topLeft.X),
            Y = (int)Math.Round(topLeft.Y),
            Height = height,
            Width = width,
            Angle = angle
        };

        double EdgeLength((PointF Start, PointF End) edge) =>
            Math.Sqrt(Math.Pow(edge.End.X - edge.Start.X, 2) + Math.Pow(edge.End.Y - edge.Start.Y, 2));

        double YMidpoint((PointF Start, PointF End) edge) => (edge.Start.Y + edge.End.Y) / 2;
    }
}
