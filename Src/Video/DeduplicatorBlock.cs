using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks.Dataflow;
using Ocr;

namespace Video;

public class DeduplicatorBlock
{
    public ITargetBlock<OcrResult> Target { get; set; }
    public ISourceBlock<OcrResult> Source { get; set; }

    public DeduplicatorBlock(int maxLevDist)
    {
        OcrResult? previousResult = null;
        int wordIdCounter = 0;

        var deduplicator = new TransformBlock<OcrResult, OcrResult>(result =>
        {
            if (previousResult is null)
            {
                previousResult = result;
                wordIdCounter += previousResult.Words.Count;
                return result;
            }

            if (result.Words.Count == 0)
            {
                return result;
            }

            var buffer = new List<(int LevDist, double EucDist, (string FromWordId, string ToWordId) Word)>();

            foreach (var fromWord in previousResult.Words)
            {
                foreach (var toWord in result.Words)
                {
                    int levDist = LevenshteinDistance(fromWord.Text, toWord.Text);

                    var fromCentroid = ComputeCentroid(fromWord.BoundingBox.Polygon);
                    var toCentroid = ComputeCentroid(toWord.BoundingBox.Polygon);
                    double eucDist = EuclideanDistance(fromCentroid, toCentroid);

                    buffer.Add((levDist, eucDist, (fromWord.Id, toWord.Id)));
                }
            }

            buffer.Sort();

            Dictionary<string, string> sameWords = [];

            foreach ((int currLevDist, double _, (string fromWordId, string toWordId)) in buffer)
            {
                if (currLevDist >= maxLevDist) break;

                if (!sameWords.ContainsKey(toWordId) && !sameWords.ContainsValue(fromWordId))
                {
                    sameWords[toWordId] = fromWordId;
                }
            }

            foreach (var word in result.Words)
            {
                if (sameWords.TryGetValue(word.Id, out string? sameWordId))
                {
                    word.Id = sameWordId;
                }
                else
                {
                    word.Id = $"word_{wordIdCounter++}";
                }
            }

            previousResult = result;

            return result;
        });

        Target = deduplicator;
        Source = deduplicator;
    }

    private static double EuclideanDistance((double X, double Y) p1, (double X, double Y) p2)
        => Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2));

    private static (double X, double Y) ComputeCentroid(List<JsonPoint> polygon)
    {
        if (polygon.Count == 0)
        {
            return (0, 0);
        }

        double sumX = 0;
        double sumY = 0;

        foreach (var point in polygon)
        {
            sumX += point.X;
            sumY += point.Y;
        }

        return (sumX / polygon.Count, sumY / polygon.Count);
    }

    public static int LevenshteinDistance(string s1, string s2)
    {
        (int m, int n)  = (s1.Length, s2.Length);

        int[,] dp =  new int[m + 1, n + 1];

        for (int i = 0; i <= m; i++)
        {
            dp[i, 0] = i;
        }

        for (int j = 0; j <= n; j++)
        {
            dp[0, j] = j;
        }

        for (int i = 1; i <= m; i++)
        {
            for (int j = 1; j <= n; j++)
            {
                if (s1[i - 1] == s2[j - 1])
                {
                    dp[i, j] = dp[i - 1, j - 1];
                }
                else
                {
                    dp[i, j] = 1 + Min(dp[i - 1, j], dp[i, j - 1], dp[i - 1, j - 1]);
                }
            }
        }

        return dp[m, n];

        T Min<T>(params T[] values) => values.Min() ?? throw new IndexOutOfRangeException();
    }
}
