// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Runtime.InteropServices;

namespace Native.Internal;

internal static partial class SpeedReaderOrt
{
    private const string LibraryName = "speedreader_ort";

    internal const int ErrorBufSize = 1024;
    internal const int MaxShapeDims = 16;

    internal enum Status
    {
        Ok = 0,
        Error = 1,
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SessionOptions
    {
        public int IntraOpNumThreads;
        public int InterOpNumThreads;
        public int EnableProfiling;
    }

    [LibraryImport(LibraryName)]
    internal static unsafe partial Status speedreader_ort_create_env(
        out SafeEnvironmentHandle env,
        byte* error);

    [LibraryImport(LibraryName)]
    internal static partial void speedreader_ort_destroy_env(IntPtr env);

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
