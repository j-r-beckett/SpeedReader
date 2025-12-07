// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

namespace Native;

public class OrtException : Exception
{
    public OrtException(string message) : base(message) { }
    public OrtException(string message, Exception innerException) : base(message, innerException) { }
}
