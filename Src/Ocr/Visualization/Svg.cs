// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

namespace Ocr.Visualization;

public class Svg
{
    private readonly string _content;

    public Svg(string content)
    {
        _content = content;
    }

    public void Save(string filename)
    {
        File.WriteAllText(filename, _content);
    }

    public async Task SaveAsync(string filename)
    {
        await File.WriteAllTextAsync(filename, _content);
    }

    public override string ToString()
    {
        return _content;
    }
}
