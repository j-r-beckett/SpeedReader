// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace SpeedReader.Native.Threading;

[SupportedOSPlatform("linux")]
internal static partial class LinuxAffinity
{
    [LibraryImport("libc", SetLastError = true)]
    private static partial int sched_setaffinity(int pid, nuint cpusetsize, ref ulong mask);

    [LibraryImport("libc", SetLastError = true)]
    private static partial int sched_getcpu();

    public static void PinToCore(int core)
    {
        const int maxCores = 4096;  // Prevent stackalloc-ing a giant array and overflowing the stack
        if (core is < 0 or >= maxCores)
            throw new ArgumentException($"Requested core {core} is out of range [0, {maxCores})");

        // Allocate a buffer with one bit per core, aligned to 8 bytes, with the core-th bit set
        var index = core / 64;  // Long within the buffer
        var bit = core % 64;  // Bit within the long
        var count = index + 1;  // Number of longs in buffer

        Span<ulong> mask = stackalloc ulong[count];
        mask.Clear();
        mask[index] = 1UL << bit;

        if (sched_setaffinity(0, (nuint)count * sizeof(ulong), ref mask[0]) != 0)
        {
            // GetLastWin32Error is cross-platform, despite the name
            throw new AffinityException($"{nameof(sched_setaffinity)} failed, errno: {Marshal.GetLastWin32Error()}");
        }
    }

    public static int GetCurrentCpu()
    {
        var cpu = sched_getcpu();
        return cpu < 0
            ? throw new AffinityException($"{nameof(sched_getcpu)} failed, errno: {Marshal.GetLastWin32Error()}")
            : cpu;
    }
}
