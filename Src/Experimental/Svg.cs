// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

namespace Experimental;

public class Svg
{
    private readonly string _content;

    public Svg(string content) => _content = content;

    public async Task Save(string filename) => await File.WriteAllTextAsync(filename, _content);

    public async Task<string> SaveAsDataUri(string? filename = null)
    {
        filename ??= $"/tmp/{Guid.NewGuid().ToString("d")[..8]}.svg";
        await Save(filename);
        return $"file://{filename}";
    }

    public override string ToString() => _content;
}
