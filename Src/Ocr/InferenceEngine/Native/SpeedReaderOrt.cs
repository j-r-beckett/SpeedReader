// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Runtime.InteropServices;

namespace Ocr.InferenceEngine.Native;

internal static partial class SpeedReaderOrt
{
    private const string LibraryName = "speedreader_ort";

    // Constants matching the C header
    internal const int ErrorBufSize = 256;
    internal const int MaxShapeDims = 16;

    // Status codes
    internal enum Status
    {
        Ok = 0,
        Error = 1,
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
    internal static unsafe partial Status speedreader_ort_create_env(
        out SafeEnvironmentHandle env,
        byte* error);

    [LibraryImport(LibraryName)]
    internal static partial void speedreader_ort_destroy_env(IntPtr env);

    // Session management
    [LibraryImport(LibraryName)]
    internal static unsafe partial Status speedreader_ort_create_session(
        SafeEnvironmentHandle env,
        byte* modelData,
        nuint modelDataSize,
        ref SessionOptions options,
        out SafeSessionHandle session,
        byte* error);

    [LibraryImport(LibraryName)]
    internal static partial void speedreader_ort_destroy_session(IntPtr session);

    // Inference execution
    [LibraryImport(LibraryName)]
    internal static unsafe partial Status speedreader_ort_run(
        SafeSessionHandle session,
        float* inputData,
        long* inputShape,
        nuint inputNdim,
        float* outputData,
        nuint outputCount,
        long* outputShape,
        nuint* outputNdim,
        byte* error);
}
