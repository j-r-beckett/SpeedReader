// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using SixLabors.Fonts;
using SpeedReader.Resources.Font;

namespace SpeedReader.Resources.Test;

public class FontsTests
{
    [Fact]
    public void CanLoadDefaultFont()
    {
        var font = EmbeddedFont.Default.Get();
        Assert.NotNull(font);
    }

    [Fact]
    public void CanLoadArialFont()
    {
        var font = EmbeddedFont.Arial.Get();
        Assert.NotNull(font);
    }

    [Theory]
    [InlineData(8f)]
    [InlineData(12f)]
    [InlineData(14f)]
    [InlineData(18f)]
    [InlineData(24f)]
    [InlineData(36f)]
    public void CanLoadFontWithCustomSize(float fontSize)
    {
        var font = EmbeddedFont.Default.Get(fontSize);
        Assert.NotNull(font);
        Assert.Equal(fontSize, font.Size);
    }

    [Theory]
    [InlineData(FontStyle.Regular)]
    [InlineData(FontStyle.Bold)]
    [InlineData(FontStyle.Italic)]
    [InlineData(FontStyle.BoldItalic)]
    public void CanLoadFontWithStyle(FontStyle fontStyle)
    {
        var font = EmbeddedFont.Default.Get(fontStyle: fontStyle);
        Assert.NotNull(font);
    }

    [Fact]
    public void CanLoadFontWithCustomSizeAndStyle()
    {
        var font = EmbeddedFont.Default.Get(fontSize: 20f, fontStyle: FontStyle.Bold);
        Assert.NotNull(font);
        Assert.Equal(20f, font.Size);
    }

    [Fact]
    public void ArialIsCached()
    {
        var font1 = EmbeddedFont.Arial;
        var font2 = EmbeddedFont.Arial;
        Assert.Same(font1, font2);
    }
}
