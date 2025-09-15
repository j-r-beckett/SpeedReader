// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System;
using System.Collections.Generic;
using System.Linq;
using SixLabors.Fonts;

namespace Resources.Test;

public class FontsTests
{
    [Theory]
    [MemberData(nameof(GetAllFontNames))]
    public void CanLoadFont(FontName fontName)
    {
        var font = Fonts.GetFont(fontName);
        Assert.NotNull(font);
    }

    [Theory]
    [MemberData(nameof(GetFontSizeTestCases))]
    public void CanLoadFontWithCustomSize(FontName fontName, float fontSize)
    {
        var font = Fonts.GetFont(fontName, fontSize);
        Assert.NotNull(font);
        Assert.Equal(fontSize, font.Size);
    }

    [Theory]
    [MemberData(nameof(GetFontStyleTestCases))]
    public void CanLoadFontWithStyle(FontName fontName, FontStyle fontStyle)
    {
        var font = Fonts.GetFont(fontName, fontStyle: fontStyle);
        Assert.NotNull(font);
    }

    public static IEnumerable<object[]> GetAllFontNames()
    {
        return Enum.GetValues<FontName>().Select(fontName => new object[] { fontName });
    }

    public static IEnumerable<object[]> GetFontSizeTestCases()
    {
        var fontNames = Enum.GetValues<FontName>();
        var sizes = new[] { 8f, 12f, 14f, 18f, 24f, 36f };

        foreach (var fontName in fontNames)
        {
            foreach (var size in sizes)
            {
                yield return new object[] { fontName, size };
            }
        }
    }

    public static IEnumerable<object[]> GetFontStyleTestCases()
    {
        var fontNames = Enum.GetValues<FontName>();
        var styles = new[] { FontStyle.Regular, FontStyle.Bold, FontStyle.Italic, FontStyle.BoldItalic };

        foreach (var fontName in fontNames)
        {
            foreach (var style in styles)
            {
                yield return new object[] { fontName, style };
            }
        }
    }
}
