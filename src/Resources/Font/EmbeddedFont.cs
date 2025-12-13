// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Collections.Concurrent;
using SixLabors.Fonts;
using SixLaborsFont = SixLabors.Fonts.Font;

namespace Resources.Font;

public class EmbeddedFont
{
    private static readonly FontCollection _fontCollection = new();
    private static readonly Lock _fontCollectionLock = new();
    private static readonly ConcurrentDictionary<string, EmbeddedFont> _cache = new();

    private readonly FontFamily _fontFamily;

    private EmbeddedFont(string resourceName)
    {
        var resource = new Resource(resourceName);
        using var resourceStream = new MemoryStream(resource.Bytes);
        lock (_fontCollectionLock)
        {
            _fontFamily = _fontCollection.Add(resourceStream);
        }
    }

    public static EmbeddedFont Default => Arial;

    public static EmbeddedFont Arial => _cache.GetOrAdd(nameof(Arial), _ => new EmbeddedFont("Font.arial.ttf"));

    public SixLaborsFont Get(float fontSize = 14f, FontStyle fontStyle = FontStyle.Regular)
        => _fontFamily.CreateFont(fontSize, fontStyle);
}
