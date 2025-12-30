// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace SpeedReader.TestUtils;

public class FileSystemUrlPublisher<T>
{
    private readonly string _baseDirectory;
    private readonly string _urlPrefix;
    private readonly ILogger<T> _logger;

    public FileSystemUrlPublisher(string baseDirectory, ILogger<T> logger, string? urlPrefix = null)
    {
        _baseDirectory = baseDirectory;
        _urlPrefix = urlPrefix ?? "file://";
        _logger = logger;

        if (!Directory.Exists(_baseDirectory))
        {
            Directory.CreateDirectory(_baseDirectory);
        }
    }

    public async Task PublishAsync(Stream data, string contentType, string description = "", CancellationToken cancellationToken = default)
    {
        var extension = GetExtensionFromContentType(contentType);
        var fileName = $"{Guid.NewGuid().ToString().Substring(0, 12)}.{extension}";
        var filePath = Path.Combine(_baseDirectory, fileName);

        await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        data.Position = 0;
        await data.CopyToAsync(fileStream, cancellationToken);

        var url = CreateUrl(filePath);

        if (!string.IsNullOrEmpty(description))
        {
            _logger.LogInformation("Published {ContentType} data ({Description}) to \n{Url}", contentType, description, url);
        }
        else
        {
            _logger.LogInformation("Published {ContentType} data to \n{Url}", contentType, url);
        }
    }

    public async Task PublishAsync(Image image, string description = "", CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream();
        await image.SaveAsync(stream, new JpegEncoder(), cancellationToken);
        await PublishAsync(stream, "image/jpeg", description, cancellationToken);
    }

    private string CreateUrl(string filePath) => $"{_urlPrefix}{filePath}";

    private static string GetExtensionFromContentType(string contentType) => contentType.ToLowerInvariant() switch
    {
        "video/webm" => "webm",
        "video/mp4" => "mp4",
        "image/jpeg" => "jpg",
        "image/png" => "png",
        _ => "bin"
    };
}
