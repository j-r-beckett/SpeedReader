using System.Text;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace Engine;

public class FileSystemUrlPublisher<T> : IUrlPublisher<T>
{
    private readonly string _baseDirectory;
    private readonly string _urlPrefix;
    private readonly ILogger<T> _logger;

    public FileSystemUrlPublisher(string baseDirectory, ILogger<T> logger, string? urlPrefix = null)
    {
        _baseDirectory = baseDirectory;
        _urlPrefix = urlPrefix ?? "file://wsl$/Ubuntu";
        _logger = logger;
        
        if (!Directory.Exists(_baseDirectory))
        {
            Directory.CreateDirectory(_baseDirectory);
        }
    }

    public async Task PublishAsync(Stream data, string contentType, string description = "", CancellationToken cancellationToken = default)
    {
        var extension = GetExtensionFromContentType(contentType);
        var fileName = $"{Guid.NewGuid()}.{extension}";
        var filePath = Path.Combine(_baseDirectory, fileName);

        await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        data.Position = 0;
        await data.CopyToAsync(fileStream, cancellationToken);

        var url = CreateUrl(filePath);
        
        if (!string.IsNullOrEmpty(description))
        {
            _logger.LogInformation("Published {ContentType} data ({Description}) to {Url}", contentType, description, url);
        }
        else
        {
            _logger.LogInformation("Published {ContentType} data to {Url}", contentType, url);
        }
    }

    public async Task PublishAsync(Image image, string description = "", CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream();
        await image.SaveAsync(stream, new JpegEncoder(), cancellationToken);
        await PublishAsync(stream, "image/jpeg", description, cancellationToken);
    }

    public async Task PublishJsonAsync(string json, string description = "", CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        await PublishAsync(stream, "application/json", description, cancellationToken);
    }

    public async Task PublishTextAsync(string text, string description = "", CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        await PublishAsync(stream, "text/plain", description, cancellationToken);
    }

    public async Task PublishChartAsync(string title, ChartData data, string description = "", CancellationToken cancellationToken = default)
    {
        var html = GenerateChartHtml(title, data);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(html));
        await PublishAsync(stream, "text/html", description, cancellationToken);
    }

    private static string GenerateChartHtml(string title, ChartData data)
    {
        var datasets = string.Join(",\n", data.Datasets.Select((ds, i) => 
            $@"{{
                label: '{ds.Label}',
                data: [{string.Join(", ", ds.Data)}],
                backgroundColor: 'rgba(54, 162, 235, 0.2)',
                borderColor: '{ds.BackgroundColor}',
                borderWidth: 2,
                fill: false,
                tension: 0.1,
                pointBackgroundColor: '{ds.BackgroundColor}',
                pointBorderColor: '#ffffff',
                pointBorderWidth: 2,
                pointRadius: 4
            }}"));

        return $@"<!DOCTYPE html>
<html>
<head>
    <title>{title}</title>
    <script src=""https://cdn.jsdelivr.net/npm/chart.js""></script>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 40px; background: #f5f5f5; }}
        .container {{ max-width: 1000px; margin: 0 auto; background: white; padding: 30px; border-radius: 8px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }}
        h1 {{ color: #333; text-align: center; margin-bottom: 30px; }}
        .chart-container {{ position: relative; height: 400px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <h1>{title}</h1>
        <div class=""chart-container"">
            <canvas id=""chart""></canvas>
        </div>
    </div>
    
    <script>
        const ctx = document.getElementById('chart').getContext('2d');
        const chart = new Chart(ctx, {{
            type: 'line',
            data: {{
                labels: [{string.Join(", ", data.Labels.Select(l => $"'{l}'"))}],
                datasets: [{datasets}]
            }},
            options: {{
                responsive: true,
                maintainAspectRatio: false,
                scales: {{
                    y: {{
                        beginAtZero: true,
                        title: {{
                            display: true,
                            text: 'Frames Per Second (FPS)'
                        }}
                    }},
                    x: {{
                        title: {{
                            display: true,
                            text: 'Time (seconds)'
                        }}
                    }}
                }},
                plugins: {{
                    legend: {{
                        display: true,
                        position: 'top'
                    }}
                }}
            }}
        }});
    </script>
</body>
</html>";
    }


    private string CreateUrl(string filePath)
    {
        return $"{_urlPrefix}{filePath}";
    }

    private static string GetExtensionFromContentType(string contentType) => contentType.ToLowerInvariant() switch
    {
        "video/webm" => "webm",
        "video/mp4" => "mp4",
        "image/jpeg" => "jpg",
        "image/png" => "png",
        "application/json" => "json",
        "text/plain" => "txt",
        "text/html" => "html",
        _ => "bin"
    };
}