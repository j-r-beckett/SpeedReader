// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using SpeedReader.Native.CpuInfo;

namespace SpeedReader.Native.Test.CpuInfo;

public class CpuTopologyTests
{
    [Fact]
    public void GetOptimalCpus_ReturnsNonEmptyList()
    {
        var cpus = CpuTopology.GetOptimalCpus();

        Assert.NotEmpty(cpus);
    }

    [Fact]
    public void GetOptimalCpus_ReturnsSameResultOnMultipleCalls()
    {
        var first = CpuTopology.GetOptimalCpus();
        var second = CpuTopology.GetOptimalCpus();

        Assert.Equal(first, second);
        Assert.Same(first, second); // Should be cached
    }

    [Fact]
    public void GetOptimalCpus_ReturnsValidCpuIndices()
    {
        var cpus = CpuTopology.GetOptimalCpus();

        foreach (var cpu in cpus)
        {
            Assert.True(cpu >= 0, $"CPU index {cpu} should be non-negative");
        }
    }
}
