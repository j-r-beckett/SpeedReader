using System;
using System.IO;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.Buffered;

namespace Resources.Test;

public class FFmpegBinariesTests
{
    [Theory]
    [InlineData("ffmpeg")]
    [InlineData("ffprobe")]
    public void GetBinaryPath_ReturnsValidExecutablePath(string binaryType)
    {
        // Arrange & Act
        var binaryPath = binaryType switch
        {
            "ffmpeg" => FFmpegBinaries.GetFFmpegPath(),
            "ffprobe" => FFmpegBinaries.GetFFprobePath(),
            _ => throw new ArgumentException($"Unknown binary type: {binaryType}")
        };

        // Assert
        Assert.NotNull(binaryPath);
        Assert.NotEmpty(binaryPath);
        Assert.True(File.Exists(binaryPath), $"Binary file should exist at path: {binaryPath}");
    }

    [Theory]
    [InlineData("ffmpeg", "-version")]
    [InlineData("ffprobe", "-version")]
    public async Task BinaryExecution_ReturnsVersionInfo_WithSuccessExitCode(string binaryType, string args)
    {
        // Arrange
        var binaryPath = binaryType switch
        {
            "ffmpeg" => FFmpegBinaries.GetFFmpegPath(),
            "ffprobe" => FFmpegBinaries.GetFFprobePath(),
            _ => throw new ArgumentException($"Unknown binary type: {binaryType}")
        };

        // Act
        var result = await Cli.Wrap(binaryPath)
            .WithArguments(args)
            .WithValidation(CommandResultValidation.None) // Don't throw on non-zero exit codes
            .ExecuteBufferedAsync();

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.NotEmpty(result.StandardOutput);
        Assert.Contains("version", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(binaryType, result.StandardOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetFFmpegPath_MultipleCalls_ReturnsSamePath()
    {
        // Arrange & Act
        var path1 = FFmpegBinaries.GetFFmpegPath();
        var path2 = FFmpegBinaries.GetFFmpegPath();

        // Assert
        Assert.Equal(path1, path2);
    }

    [Fact]
    public void GetFFprobePath_MultipleCalls_ReturnsSamePath()
    {
        // Arrange & Act
        var path1 = FFmpegBinaries.GetFFprobePath();
        var path2 = FFmpegBinaries.GetFFprobePath();

        // Assert
        Assert.Equal(path1, path2);
    }

    [Fact]
    public void GetFFmpegPath_And_GetFFprobePath_ReturnDifferentPaths()
    {
        // Arrange & Act
        var ffmpegPath = FFmpegBinaries.GetFFmpegPath();
        var ffprobePath = FFmpegBinaries.GetFFprobePath();

        // Assert
        Assert.NotEqual(ffmpegPath, ffprobePath);
    }
}