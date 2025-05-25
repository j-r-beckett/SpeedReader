using SixLabors.ImageSharp;

namespace Engine;

public interface IUrlPublisher<T>
{
    Task PublishAsync(Stream data, string contentType, string description = "", CancellationToken cancellationToken = default);
    Task PublishAsync(Image image, string description = "", CancellationToken cancellationToken = default);
    Task PublishJsonAsync(string json, string description = "", CancellationToken cancellationToken = default);
    Task PublishTextAsync(string text, string description = "", CancellationToken cancellationToken = default);
}