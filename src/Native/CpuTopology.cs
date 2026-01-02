// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Runtime.InteropServices;
using SpeedReader.Native.Internal;

namespace SpeedReader.Native;

public static class CpuTopology
{
    private static readonly Lazy<int[]> _optimalCpus = new(GetOptimalCpusCore);

    /// <summary>
    /// Gets the optimal CPU indices for affinitized inference threads.
    /// One CPU per L2 cache, primary threads only (no hyperthreads).
    /// Sorted by frequency descending (P-cores first, then E-cores).
    /// </summary>
    /// <remarks>
    /// The result is cached after the first call.
    /// Thread-safe.
    /// </remarks>
    public static IReadOnlyList<int> GetOptimalCpus() => _optimalCpus.Value;

    private static unsafe int[] GetOptimalCpusCore()
    {
        SpeedReaderCpuInfo.OptimalCpus result;
        var errorBuffer = stackalloc byte[SpeedReaderCpuInfo.ErrorBufSize];

        var status = SpeedReaderCpuInfo.speedreader_cpuinfo_get_optimal_cpus(&result, errorBuffer);

        if (status != SpeedReaderCpuInfo.Status.Ok)
        {
            var errorMessage = Marshal.PtrToStringUTF8((IntPtr)errorBuffer);
            throw new CpuInfoException($"Failed to get optimal CPUs: {errorMessage}");
        }

        var cpus = new int[result.Count];
        for (int i = 0; i < result.Count; i++)
        {
            cpus[i] = result.CpuIndices[i];
        }

        return cpus;
    }
}
