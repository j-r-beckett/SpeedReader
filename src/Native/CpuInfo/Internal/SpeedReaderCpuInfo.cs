// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Runtime.InteropServices;

namespace SpeedReader.Native.CpuInfo.Internal;

internal static partial class SpeedReaderCpuInfo
{
    private const string LibraryName = "speedreader_cpuinfo";

    internal const int ErrorBufSize = 256;
    internal const int MaxCpus = 256;

    internal enum Status
    {
        Ok = 0,
        Error = 1,
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct OptimalCpus
    {
        public fixed int CpuIndices[MaxCpus];
        public int Count;
    }

    [LibraryImport(LibraryName)]
    internal static unsafe partial Status speedreader_cpuinfo_get_optimal_cpus(
        OptimalCpus* result,
        byte* error);
}
