// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

namespace SpeedReader.Native.Threading;

public class AffinityException : Exception
{
    public AffinityException(string message) : base(message) { }
}
