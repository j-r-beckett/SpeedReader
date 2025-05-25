using System.Text;
using Engine;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Engine.Test;

public class FileSystemUrlPublisherTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly FileSystemUrlPublisher<FileSystemUrlPublisherTests> _publisher;
    private readonly CapturingLogger<FileSystemUrlPublisherTests> _logger;

    public FileSystemUrlPublisherTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "url-publisher-tests", Guid.NewGuid().ToString());
        _logger = new CapturingLogger<FileSystemUrlPublisherTests>();
        _publisher = new FileSystemUrlPublisher<FileSystemUrlPublisherTests>(_testDirectory, _logger);
    }

    [Fact]
    public async Task PublishAsync_Stream_CreatesFileAndLogs()
    {
        var testData = "Hello, World!"u8.ToArray();
        using var stream = new MemoryStream(testData);

        await _publisher.PublishAsync(stream, "text/plain");

        // Verify file was created
        var createdFiles = Directory.GetFiles(_testDirectory, "*.txt");
        createdFiles.Should().HaveCount(1);
        
        var savedData = await File.ReadAllBytesAsync(createdFiles[0]);
        savedData.Should().Equal(testData);

        // Verify logging
        var logEntry = _logger.GetLastLogEntry();
        logEntry.Should().NotBeNull();
        logEntry!.Message.Should().Contain("text/plain");
        logEntry.Message.Should().Contain("file://wsl$/Ubuntu");
    }

    [Fact]
    public async Task PublishAsync_WithDescription_IncludesDescriptionInLog()
    {
        var testData = "test with description"u8.ToArray();
        using var stream = new MemoryStream(testData);

        await _publisher.PublishAsync(stream, "text/plain", "test file for debugging");

        var logEntry = _logger.GetLastLogEntry();
        logEntry.Should().NotBeNull();
        logEntry!.Message.Should().Contain("test file for debugging");
        logEntry.Message.Should().Contain("text/plain");
    }

    [Fact]
    public async Task PublishJsonAsync_CreatesFileAndLogs()
    {
        var jsonData = """{"test": "value"}""";

        await _publisher.PublishJsonAsync(jsonData, "test config");

        // Verify file was created
        var createdFiles = Directory.GetFiles(_testDirectory, "*.json");
        createdFiles.Should().HaveCount(1);
        
        var savedJson = await File.ReadAllTextAsync(createdFiles[0]);
        savedJson.Should().Be(jsonData);

        // Verify logging includes description
        var logEntry = _logger.GetLastLogEntry();
        logEntry.Should().NotBeNull();
        logEntry!.Message.Should().Contain("test config");
        logEntry.Message.Should().Contain("application/json");
    }

    [Fact]
    public async Task PublishTextAsync_CreatesFileAndLogs()
    {
        var textData = "This is test text";

        await _publisher.PublishTextAsync(textData);

        // Verify file was created
        var createdFiles = Directory.GetFiles(_testDirectory, "*.txt");
        createdFiles.Should().HaveCount(1);
        
        var savedText = await File.ReadAllTextAsync(createdFiles[0]);
        savedText.Should().Be(textData);

        // Verify logging
        var logEntry = _logger.GetLastLogEntry();
        logEntry.Should().NotBeNull();
        logEntry!.Message.Should().Contain("text/plain");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }
}