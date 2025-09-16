// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Collections.Concurrent;
using SixLabors.Fonts;

namespace Resources;

public static class Fonts
{
    private static readonly FontCollection _collection = new();
    private static readonly ConcurrentDictionary<FontName, bool> _loadedFonts = new();
    private static readonly object _lock = new();

    public static Font GetFont(FontName fontName = FontName.Arial, float fontSize = 14f, FontStyle fontStyle = FontStyle.Regular)
    {
        EnsureInitialized(fontName);

        var familyName = GetFamilyName(fontName);
        var family = _collection.Get(familyName);
        return family.CreateFont(fontSize, fontStyle);
    }

    private static void EnsureInitialized(FontName fontName)
    {
        if (_loadedFonts.ContainsKey(fontName))
        {
            return;
        }

        lock (_lock)
        {
            if (_loadedFonts.ContainsKey(fontName))
            {
                return;
            }

            var resourceName = GetResourceName(fontName);
            var fontBytes = Resource.GetBytes(resourceName);
            using var stream = new MemoryStream(fontBytes);
            _collection.Add(stream);

            _loadedFonts[fontName] = true;
        }
    }

    private static string GetResourceName(FontName fontName) => fontName switch
    {
        FontName.Arial => "arial.ttf",
        _ => throw new ArgumentException($"Unknown font name: {fontName}")
    };

    private static string GetFamilyName(FontName fontName) => fontName switch
    {
        FontName.Arial => "Arial",
        _ => throw new ArgumentException($"Unknown font name: {fontName}")
    };
}

public enum FontName
{
    Arial
}
