// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Collections.Concurrent;
using System.Reflection;

namespace Resources;

public static class Resource
{
    private static readonly ConcurrentDictionary<string, byte[]> _cache = new();

    public static byte[] GetBytes(string resourceName) => _cache.GetOrAdd(resourceName, LoadResource);

    public static string GetString(string resourceName)
    {
        var bytes = GetBytes(resourceName);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    private static byte[] LoadResource(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();

        using var stream = assembly.GetManifestResourceStream($"Resources.{resourceName}") ?? throw new FileNotFoundException($"Embedded resource 'Resources.{resourceName}' not found");
        var bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);

        return bytes;
    }
}
