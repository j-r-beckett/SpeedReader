// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Runtime.InteropServices;

namespace SpeedReader.Native.Onnx.Internal;

internal static class OrtEnvironment
{
    private static readonly Lazy<SafeEnvironmentHandle> _sharedEnvironment = new(CreateEnvironment);

    internal static SafeEnvironmentHandle Instance => _sharedEnvironment.Value;

    private static unsafe SafeEnvironmentHandle CreateEnvironment()
    {
        var errorBuffer = stackalloc byte[SpeedReaderOrt.ErrorBufSize];

        var status = SpeedReaderOrt.speedreader_ort_create_env(out var env, errorBuffer);

        if (status != SpeedReaderOrt.Status.Ok)
        {
            var errorMessage = Marshal.PtrToStringUTF8((IntPtr)errorBuffer);
            throw new OrtException($"Failed to create ONNX Runtime environment: {errorMessage}");
        }

        return env;
    }
}
