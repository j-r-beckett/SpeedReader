using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp;

namespace Video;

public record ChartData(string[] Labels, ChartDataset[] Datasets);
public record ChartDataset(string Label, double[] Data, string BackgroundColor = "#36A2EB");

public interface IUrlPublisher<T>
{
    Task PublishAsync(Stream data, string contentType, string description = "", CancellationToken cancellationToken = default);
    Task PublishAsync(Image image, string description = "", CancellationToken cancellationToken = default);
    Task PublishJsonAsync(string json, string description = "", CancellationToken cancellationToken = default);
    Task PublishTextAsync(string text, string description = "", CancellationToken cancellationToken = default);
    Task PublishChartAsync(string title, ChartData data, string description = "", CancellationToken cancellationToken = default);
}
