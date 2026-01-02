// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

namespace SpeedReader.Native.Test;

public class OrtValueTests
{
    [Fact]
    public void Create_WithValidDataAndShape_Succeeds()
    {
        float[] data = [1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f];
        int[] shape = [2, 3];

        var ortValue = OrtValue.Create(data, shape);

        Assert.Equal(6, ortValue.Data.Length);
        Assert.Equal([2, 3], ortValue.Shape);
    }

    [Fact]
    public void Create_WithNullShape_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => OrtValue.Create(new float[6], null!));

    [Fact]
    public void Create_WithMismatchedDataLength_ThrowsArgumentException()
    {
        float[] data = [1.0f, 2.0f, 3.0f];
        int[] shape = [2, 3];

        var exception = Assert.Throws<ArgumentException>(() => OrtValue.Create(data, shape));

        Assert.Contains("Data length 3 does not match shape [2, 3]", exception.Message);
    }

    [Fact]
    public void Create_MakesDefensiveCopyOfShape()
    {
        float[] data = [1.0f, 2.0f, 3.0f, 4.0f];
        int[] shape = [2, 2];

        var ortValue = OrtValue.Create(data, shape);
        shape[0] = 99;

        Assert.Equal([2, 2], ortValue.Shape);
    }

    [Fact]
    public void Create_With1DShape_Succeeds()
    {
        float[] data = [1.0f, 2.0f, 3.0f];
        int[] shape = [3];

        var ortValue = OrtValue.Create(data, shape);

        Assert.Equal(3, ortValue.Data.Length);
        Assert.Equal([3], ortValue.Shape);
    }

    [Fact]
    public void Create_With4DShape_Succeeds()
    {
        var data = new float[2 * 3 * 4 * 5];
        int[] shape = [2, 3, 4, 5];

        var ortValue = OrtValue.Create(data, shape);

        Assert.Equal(120, ortValue.Data.Length);
        Assert.Equal([2, 3, 4, 5], ortValue.Shape);
    }

    [Fact]
    public void Create_WithZeroDimension_Succeeds()
    {
        float[] data = [];
        int[] shape = [0, 3];

        var ortValue = OrtValue.Create(data, shape);

        Assert.Equal(0, ortValue.Data.Length);
    }

    [Fact]
    public void Data_ReturnsOriginalMemory()
    {
        float[] data = [1.0f, 2.0f, 3.0f];
        var ortValue = OrtValue.Create(data, [3]);

        data[0] = 99.0f;

        Assert.Equal(99.0f, ortValue.Data.Span[0]);
    }
}
