using Clipper2Lib;

namespace Ocr.Algorithms;

public static class Dilation
{
    private const double DilationRatio = 1.5;
    private const double MinimumArea = 9.0;

    public static List<(int X, int Y)[]> DilatePolygons((int X, int Y)[][] polygons)
    {
        List<(int X, int Y)[]> dilatedPolygons = [];

        foreach (var polygon in polygons)
        {
            var dilated = DilatePolygon(polygon.ToList());
            if (dilated.Count > 0)
            {
                dilatedPolygons.Add(dilated.ToArray());
            }
        }

        return dilatedPolygons;
    }

    public static List<(int X, int Y)> DilatePolygon(List<(int X, int Y)> polygon)
    {
        if (polygon.Count < 3)
        {
            return [];
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
            return [];
        }

        double offset = area * DilationRatio / perimeter;

        var clipperPath = Clipper.Path64(clipperPathD);
        var clipperOffset = new ClipperOffset();
        clipperOffset.AddPath(clipperPath, JoinType.Round, EndType.Polygon);

        var solution = new Paths64();
        clipperOffset.Execute(offset, solution);

        if (solution.Count == 0 || solution[0].Count < 3)
        {
            return [];
        }

        var dilatedPolygon = new List<(int X, int Y)>(solution[0].Count);
        for (int i = 0; i < solution[0].Count; i++)
        {
            var point = solution[0][i];
            dilatedPolygon.Add(((int)point.X, (int)point.Y));
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
