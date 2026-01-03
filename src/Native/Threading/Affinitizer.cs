// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

namespace SpeedReader.Native.Threading;

public class Affinitizer
{
    public static void PinToCore(int core)
    {
        if (OperatingSystem.IsLinux())
            LinuxAffinity.PinToCore(core);
        else
            throw new PlatformNotSupportedException();
    }

    public static int GetCurrentCpu() => OperatingSystem.IsLinux()
        ? LinuxAffinity.GetCurrentCpu()
        : throw new PlatformNotSupportedException();
}
