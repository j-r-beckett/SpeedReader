// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using SpeedReader.Native.Onnx;

namespace SpeedReader.Native.Test.Onnx;

public class SessionOptionsTests
{
    [Fact]
    public void Default_HasExpectedValues()
    {
        var options = new SessionOptions();

        Assert.Equal(1, options.IntraOpNumThreads);
        Assert.Equal(1, options.InterOpNumThreads);
        Assert.False(options.EnableProfiling);
    }

    [Fact]
    public void WithIntraOpThreads_NegativeCount_ThrowsArgumentOutOfRangeException() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new SessionOptions().WithIntraOpThreads(-1));

    [Fact]
    public void WithInterOpThreads_NegativeCount_ThrowsArgumentOutOfRangeException() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new SessionOptions().WithInterOpThreads(-1));

    [Fact]
    public void WithIntraOpThreads_Zero_Succeeds()
    {
        var options = new SessionOptions().WithIntraOpThreads(0);
        Assert.Equal(0, options.IntraOpNumThreads);
    }

    [Fact]
    public void WithInterOpThreads_Zero_Succeeds()
    {
        var options = new SessionOptions().WithInterOpThreads(0);
        Assert.Equal(0, options.InterOpNumThreads);
    }

    [Fact]
    public void WithProfiling_True_EnablesProfiling()
    {
        var options = new SessionOptions().WithProfiling();
        Assert.True(options.EnableProfiling);
    }

    [Fact]
    public void WithProfiling_False_DisablesProfiling()
    {
        var options = new SessionOptions().WithProfiling(false);
        Assert.False(options.EnableProfiling);
    }

    [Fact]
    public void ChainedOptions_SetsAllValues()
    {
        var options = new SessionOptions()
            .WithIntraOpThreads(4)
            .WithInterOpThreads(2)
            .WithProfiling();

        Assert.Equal(4, options.IntraOpNumThreads);
        Assert.Equal(2, options.InterOpNumThreads);
        Assert.True(options.EnableProfiling);
    }
}
