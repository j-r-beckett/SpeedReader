using System.Collections.Concurrent;
using System.Reflection;

namespace Resources;

public static class Resource
{
    private static readonly ConcurrentDictionary<string, byte[]> _cache = new();

    public static byte[] GetBytes(string resourceName)
    {
        return _cache.GetOrAdd(resourceName, LoadResource);
    }

    private static byte[] LoadResource(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        
        using var stream = assembly.GetManifestResourceStream($"Resources.{resourceName}");
        if (stream == null)
            throw new FileNotFoundException($"Embedded resource 'Resources.{resourceName}' not found");

        var bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);
        
        return bytes;
    }
}