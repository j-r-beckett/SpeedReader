// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

namespace Native.Test;

public class InferenceSessionTests
{
    [Fact]
    public void CreateSession_WithInvalidModelData_ThrowsOrtException()
    {
        byte[] invalidModelData = [0x00, 0x01, 0x02, 0x03];

        var exception = Assert.Throws<OrtException>(() => new InferenceSession(invalidModelData));

        Assert.Contains("Failed to create session", exception.Message);
        Assert.Contains("protobuf parsing failed", exception.Message);
    }
}
