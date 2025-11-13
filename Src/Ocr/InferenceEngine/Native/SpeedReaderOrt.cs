// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Runtime.InteropServices;

namespace Ocr.InferenceEngine.Native;

internal static partial class SpeedReaderOrt
{
    private const string LibraryName = "speedreader_ort";

    // Status codes
    internal enum Status
    {
        Ok = 0,
        Unknown = 1,
        InvalidArgument = 2,
        Truncated = 3,
    }

    // Buffer structures
    [StructLayout(LayoutKind.Sequential)]
    internal struct StringBuf
    {
        public IntPtr Buffer;
        public nuint Capacity;
        public nuint Length;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct FloatBuf
    {
        public IntPtr Buffer;
        public nuint Capacity;
        public nuint Length;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Int64Buf
    {
        public IntPtr Buffer;
        public nuint Capacity;
        public nuint Length;
    }

    // Session options
    [StructLayout(LayoutKind.Sequential)]
    internal struct SessionOptions
    {
        public int IntraOpNumThreads;
        public int InterOpNumThreads;
        public int EnableProfiling;  // 0 = disabled, 1 = enabled
    }

    // Environment management
    [LibraryImport(LibraryName)]
    internal static partial Status speedreader_ort_create_env(out SafeEnvironmentHandle env, ref StringBuf error);

    [LibraryImport(LibraryName)]
    internal static partial void speedreader_ort_destroy_env(IntPtr env);

    // Session management
    [LibraryImport(LibraryName)]
    internal static partial Status speedreader_ort_create_session(
        SafeEnvironmentHandle env,
        IntPtr modelData,
        nuint modelDataLength,
        ref SessionOptions options,
        out SafeSessionHandle session,
        ref StringBuf error);

    [LibraryImport(LibraryName)]
    internal static partial void speedreader_ort_destroy_session(IntPtr session);

    // Inference execution
    [LibraryImport(LibraryName)]
    internal static partial Status speedreader_ort_run(
        SafeSessionHandle session,
        IntPtr inputData,
        IntPtr inputShape,
        nuint inputShapeLength,
        ref FloatBuf outputData,
        ref Int64Buf outputShape,
        ref StringBuf error);
}
