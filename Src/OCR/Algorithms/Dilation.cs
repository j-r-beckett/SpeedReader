using Clipper2Lib;

namespace OCR.Algorithms;

public static class Dilation
{
    private const double DilationRatio = 1.5;
    private const double MinimumArea = 9.0;

    public static (int X, int Y)[][] DilatePolygons((int X, int Y)[][] polygons)
    {
        List<(int X, int Y)[]> dilatedPolygons = [];

        foreach (var polygon in polygons)
        {
            var dilated = DilatePolygon(polygon);
            if (dilated != null)
            {
                dilatedPolygons.Add(dilated);
            }
        }

        return dilatedPolygons.ToArray();
    }

    public static (int X, int Y)[]? DilatePolygon((int X, int Y)[] polygon)
    {
        if (polygon.Length < 3)
        {
            return null;
        }

        var clipperPathD = new PathD();
        foreach (var point in polygon)
        {
            clipperPathD.Add(new PointD(point.X, point.Y));
        }

        double area = Math.Abs(Clipper.Area(clipperPathD));
        double perimeter = CalculatePerimeter(clipperPathD);

        if (perimeter <= 0 || area < MinimumArea)
        {
            return null;
        }

        double offset = area * DilationRatio / perimeter;

        var clipperPath = Clipper.Path64(clipperPathD);
        var clipperOffset = new ClipperOffset();
        clipperOffset.AddPath(clipperPath, JoinType.Round, EndType.Polygon);

        var solution = new Paths64();
        clipperOffset.Execute(offset, solution);

        if (solution.Count == 0 || solution[0].Count < 3)
        {
            return null;
        }

        var dilatedPolygon = new (int X, int Y)[solution[0].Count];
        for (int i = 0; i < solution[0].Count; i++)
        {
            var point = solution[0][i];
            dilatedPolygon[i] = ((int)point.X, (int)point.Y);
        }

        return dilatedPolygon;
    }

    private static double CalculatePerimeter(PathD path)
    {
        double perimeter = 0;
        for (int i = 0; i < path.Count; i++)
        {
            var current = path[i];
            var next = path[(i + 1) % path.Count];
            var dx = next.x - current.x;
            var dy = next.y - current.y;
            perimeter += Math.Sqrt(dx * dx + dy * dy);
        }
        return perimeter;
    }
}
