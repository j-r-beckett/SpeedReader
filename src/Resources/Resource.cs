// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Reflection;

namespace SpeedReader.Resources;

public record Resource
{
    private readonly Lazy<byte[]> _bytes;

    // Throws ResourceNotFoundException if the resource does not exist
    public Resource(string resourceName)
    {
        using var stream = GetResourceStream(resourceName);  // Done for side effect purposes, throws if resource DNE
        _bytes = new Lazy<byte[]>(() => LoadResource(resourceName));
    }

    public byte[] Bytes => _bytes.Value;

    private static byte[] LoadResource(string resourceName)
    {
        using var stream = GetResourceStream(resourceName);
        var bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }

    private static Stream GetResourceStream(string resourceName)
    {
        var fullName = $"SpeedReader.Resources.{resourceName}";
        var assembly = Assembly.GetExecutingAssembly();

        // The Stream returned by GetManifestResourceStream is implemented as a pointer to the embedded resource
        // bytes in the text segment of process memory.
        // See https://github.com/dotnet/runtime/blob/767be2a5fc0ca26a4059883ae22a5d4522086cc6/src/mono/mono/metadata/icall.c#L4824
        return assembly.GetManifestResourceStream(fullName) ?? throw new ResourceNotFoundException(resourceName);
    }
}

public class ResourceNotFoundException : Exception
{
    public ResourceNotFoundException(string resourceName) : base($"Embedded resource '{resourceName}' not found") { }
}
