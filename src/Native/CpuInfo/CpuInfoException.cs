// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

namespace SpeedReader.Native.CpuInfo;

public class CpuInfoException : Exception
{
    public CpuInfoException(string message) : base(message) { }
    public CpuInfoException(string message, Exception innerException) : base(message, innerException) { }
}
