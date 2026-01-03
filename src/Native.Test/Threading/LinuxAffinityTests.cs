// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Runtime.Versioning;
using SpeedReader.Native.Threading;

namespace SpeedReader.Native.Test.Threading;

[SupportedOSPlatform("linux")]
public class LinuxAffinityTests
{
    [Fact]
    public void PinToCore_NegativeCore_ThrowsArgumentException() => Assert.Throws<ArgumentException>(() => LinuxAffinity.PinToCore(-1));

    [Fact]
    public void PinToCore_CoreAtMaxLimit_ThrowsArgumentException() => Assert.Throws<ArgumentException>(() => LinuxAffinity.PinToCore(4096));

    [Fact]
    public void PinToCore_CoreBeyondMaxLimit_ThrowsArgumentException() => Assert.Throws<ArgumentException>(() => LinuxAffinity.PinToCore(5000));

    [Fact]
    public void PinToCore_CoreZero_Succeeds()
    {
        var exception = Record.Exception(() => LinuxAffinity.PinToCore(0));

        Assert.Null(exception);
    }

    [Fact]
    public void PinToCore_NonExistentCore_ThrowsAffinityException() => Assert.Throws<AffinityException>(() => LinuxAffinity.PinToCore(3999));

    [Fact]
    public void GetCurrentCpu_ReturnsNonNegativeValue()
    {
        var cpu = LinuxAffinity.GetCurrentCpu();

        Assert.True(cpu >= 0, $"Expected non-negative CPU index, got {cpu}");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void PinToCore_ThenGetCurrentCpu_ReturnsExpectedCore(int core)
    {
        // Assumes at least two cores
        LinuxAffinity.PinToCore(core);
        var cpu = LinuxAffinity.GetCurrentCpu();

        Assert.Equal(core, cpu);
    }
}
