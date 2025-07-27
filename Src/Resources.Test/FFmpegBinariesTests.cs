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
    public async Task SystemPathResolution_WhenSystemBinaryExists_ReturnsSystemPath(string binaryType, string args)
    {
        // Arrange - First verify system binary exists using our own implementation of 'which'
        var systemPath = await FindSystemBinaryPath(binaryType);

        // Act - Get path using system resolution enabled (default)
        var resolvedPath = binaryType switch
        {
            "ffmpeg" => FFmpegBinaries.GetFFmpegPath(useSystemPath: true),
            "ffprobe" => FFmpegBinaries.GetFFprobePath(useSystemPath: true),
            _ => throw new ArgumentException($"Unknown binary type: {binaryType}")
        };

        // Assert - Should return the system path we found
        Assert.Equal(systemPath, resolvedPath);

        // Verify the system binary actually works
        var result = await Cli.Wrap(resolvedPath)
            .WithArguments(args)
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync();

        Assert.Equal(0, result.ExitCode);
        Assert.NotEmpty(result.StandardOutput);
        Assert.Contains("version", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(binaryType, result.StandardOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("ffmpeg", "-version")]
    [InlineData("ffprobe", "-version")]
    public async Task BinaryExecution_DefaultBehavior_ReturnsVersionInfo(string binaryType, string args)
    {
        // Arrange - Use default behavior (system path enabled)
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

    [Theory]
    [InlineData("ffmpeg", "-version")]
    [InlineData("ffprobe", "-version")]
    public async Task EmbeddedBinaryExecution_SkipsSystemPath_ExtractsAndExecutes(string binaryType, string args)
    {
        // Arrange - Get system binary path for comparison
        var systemPath = await FindSystemBinaryPath(binaryType);

        // Arrange - explicitly skip system PATH to force embedded resource extraction
        var binaryPath = binaryType switch
        {
            "ffmpeg" => FFmpegBinaries.GetFFmpegPath(useSystemPath: false),
            "ffprobe" => FFmpegBinaries.GetFFprobePath(useSystemPath: false),
            _ => throw new ArgumentException($"Unknown binary type: {binaryType}")
        };

        // Act
        var result = await Cli.Wrap(binaryPath)
            .WithArguments(args)
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync();

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.NotEmpty(result.StandardOutput);
        Assert.Contains("version", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(binaryType, result.StandardOutput, StringComparison.OrdinalIgnoreCase);

        // Verify the binary was extracted to expected location (not system PATH)
        Assert.False(binaryPath.StartsWith("/usr/") || binaryPath.StartsWith("/bin/"));

        // Verify the embedded binary path is different from system binary path
        Assert.NotEqual(systemPath, binaryPath);
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

    /// <summary>
    /// Test implementation of system binary lookup using 'which' command
    /// </summary>
    private static async Task<string> FindSystemBinaryPath(string binaryName)
    {
        var result = await Cli.Wrap("which")
            .WithArguments(binaryName)
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync();

        if (result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            var path = result.StandardOutput.Trim();
            if (File.Exists(path))
            {
                return path;
            }
        }

        Assert.Fail($"System {binaryName} not found in PATH. Please install FFmpeg on the host system to run this test. " +
                   $"On Ubuntu/Debian: sudo apt install ffmpeg. On other systems, install FFmpeg from https://ffmpeg.org/");
        throw new InvalidOperationException("This should never be reached");
    }
}
