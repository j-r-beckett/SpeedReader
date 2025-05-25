using SixLabors.ImageSharp;

namespace Engine.Test;

public static class MediaSaver
{
    private static readonly string DebugDir = $"{Directory.GetCurrentDirectory()}/out/debug";

    static MediaSaver()
    {
        if (!Directory.Exists(DebugDir))
        {
            Directory.CreateDirectory(DebugDir);
        }
    }

    public static async Task<string> SaveImage(this Image image, CancellationToken cancellationToken)
    {
        var path = $"{DebugDir}/{Guid.NewGuid()}.jpg";
        await image.SaveAsync(path, cancellationToken);
        return $"file://wsl$/Ubuntu{path}";
    }

    public static async Task<string> SaveVideoStream(this Stream stream, string extension, CancellationToken cancellationToken)
    {
        var path = $"{DebugDir}/{Guid.NewGuid()}.{extension}";
        await using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);
        await stream.CopyToAsync(fileStream, cancellationToken);
        return $"file://wsl$/Ubuntu{path}";
    }
}