
// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Collections.Concurrent;
using System.Reflection;
using System.Text;

namespace Resources;

public class ResourceAccessor
{
    private readonly ConcurrentDictionary<string, byte[]> _cache = new();

    public string GetResourceAsString(string resourceName) => Encoding.UTF8.GetString(GetResourceAsBytes(resourceName));

    public byte[] GetResourceAsBytes(string resourceName) => _cache.GetOrAdd(resourceName, LoadResource);

    private static byte[] LoadResource(string resourceName)
    {
        var fullName = $"Resources.{resourceName}";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(fullName) ?? throw new ResourceNotFoundException(fullName);
        var bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }
}

public class ResourceNotFoundException : Exception
{
    public ResourceNotFoundException(string fullName) : base($"Embedded resource '{fullName}' not found") { }
}
