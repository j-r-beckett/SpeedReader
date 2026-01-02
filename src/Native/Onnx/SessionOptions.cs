// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

namespace SpeedReader.Native.Onnx;

public sealed class SessionOptions
{
    public int IntraOpNumThreads { get; private set; } = 1;
    public int InterOpNumThreads { get; private set; } = 1;
    public bool EnableProfiling { get; private set; }

    public SessionOptions WithIntraOpThreads(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        IntraOpNumThreads = count;
        return this;
    }

    public SessionOptions WithInterOpThreads(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        InterOpNumThreads = count;
        return this;
    }

    public SessionOptions WithProfiling(bool enable = true)
    {
        EnableProfiling = enable;
        return this;
    }

    internal Internal.SpeedReaderOrt.SessionOptions ToNative() => new()
    {
        IntraOpNumThreads = IntraOpNumThreads,
        InterOpNumThreads = InterOpNumThreads,
        EnableProfiling = EnableProfiling ? 1 : 0
    };
}
