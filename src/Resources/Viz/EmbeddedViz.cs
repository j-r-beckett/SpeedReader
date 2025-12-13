// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Text;

namespace Resources.Viz;

public class EmbeddedViz
{
    private readonly Resource _template = new("Viz.template.svg");

    public string Template => Encoding.UTF8.GetString(_template.Bytes);
}
