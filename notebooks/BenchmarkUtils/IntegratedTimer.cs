using System.Diagnostics;
using System.Text;

namespace BenchmarkUtils;

public static class IntegratedTimer
{
    private static readonly DateTime BaseTime = DateTime.UtcNow;
    private static readonly long BaseTicks = Stopwatch.GetTimestamp();
    private static DateTime Now() => BaseTime + Stopwatch.GetElapsedTime(BaseTicks);

    public static Token Start() => new Token();

    public static void Stop(Token token)
    {
        var end = Now();
        var sb = new StringBuilder();
        sb.Append("{\"start\":\"");
        sb.Append(token.StartTime.ToString("O"));
        sb.Append("\",\"end\":\"");
        sb.Append(end.ToString("O"));
        sb.Append("\",\"tags\":{");
        var first = true;
        foreach (var (key, value) in token.Tags)
        {
            if (!first) sb.Append(',');
            first = false;
            sb.Append('"');
            sb.Append(EscapeJson(key));
            sb.Append("\":\"");
            sb.Append(EscapeJson(value));
            sb.Append('"');
        }
        sb.Append("}}");
        Console.WriteLine(sb.ToString());
    }

    private static string EscapeJson(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    public record Token
    {
        internal DateTime StartTime { get; }
        internal Token() => StartTime = Now();
        public Dictionary<string, string> Tags { get; init; } = new();
    }
}
