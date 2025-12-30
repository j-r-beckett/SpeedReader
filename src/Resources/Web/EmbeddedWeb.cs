// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Text;

namespace SpeedReader.Resources.Web;

public class EmbeddedWeb
{
    private readonly Resource _demo = new("Web.demo.html");

    public string DemoHtml => Encoding.UTF8.GetString(_demo.Bytes);
}
