using SpeedReader.Ocr.InferenceEngine;

namespace BenchmarkUtils;

public class UnrecognizedFlagException : Exception
{
    public UnrecognizedFlagException(string name)
        : base($"Did not recognize flag: --{name}") { }
}

public class BenchmarkArgs
{
    private readonly Dictionary<string, string[]> _flags = new();

    private BenchmarkArgs() { }

    public static BenchmarkArgs Parse(string[] args)
    {
        var result = new BenchmarkArgs();

        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--"))
                continue;

            var name = args[i][2..];
            var values = new List<string>();

            // Collect values until next flag or end
            while (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
            {
                i++;
                values.Add(args[i]);
            }

            result._flags[name] = values.ToArray();
        }

        return result;
    }

    public T GetFlag<T>(string name)
    {
        if (!_flags.TryGetValue(name, out var values))
            throw new UnrecognizedFlagException(name);

        return ConvertValues<T>(values);
    }

    public T GetFlag<T>(string name, T defaultValue)
    {
        if (!_flags.TryGetValue(name, out var values))
            return defaultValue;

        return ConvertValues<T>(values);
    }

    private static T ConvertValues<T>(string[] values)
    {
        var type = typeof(T);

        // Bool: presence = true
        if (type == typeof(bool))
            return (T)(object)true;

        // Scalar types
        if (type == typeof(string))
            return (T)(object)(values.Length > 0 ? values[0] : "");

        if (type == typeof(int))
            return (T)(object)int.Parse(values[0]);

        if (type == typeof(double))
            return (T)(object)double.Parse(values[0]);

        if (type == typeof(Model))
        {
            return (T)(object)(values[0].ToLowerInvariant() switch
            {
                "dbnet" => Model.DbNet,
                "svtr" => Model.Svtr,
                var m => throw new ArgumentException($"Unknown model: {m}")
            });
        }

        // Array types
        if (type == typeof(string[]))
            return (T)(object)values;

        if (type == typeof(int[]))
            return (T)(object)values.Select(int.Parse).ToArray();

        if (type == typeof(double[]))
            return (T)(object)values.Select(double.Parse).ToArray();

        throw new NotSupportedException($"Unsupported flag type: {type.Name}");
    }
}
