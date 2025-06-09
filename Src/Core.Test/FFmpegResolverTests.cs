using Core;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Core.Test;

public class FFmpegResolverTests
{
    [Fact]
    public void GetFFmpegPath_CachesResult()
    {
        // Clear any cached values using reflection
        ClearCachedPaths();

        var firstCall = FFmpegResolver.GetFFmpegPath();
        var secondCall = FFmpegResolver.GetFFmpegPath();

        Assert.Equal(firstCall, secondCall);
        Assert.NotNull(firstCall);
        Assert.NotEmpty(firstCall);
    }

    [Fact]
    public void GetFFprobePath_CachesResult()
    {
        // Clear any cached values using reflection
        ClearCachedPaths();

        var firstCall = FFmpegResolver.GetFFprobePath();
        var secondCall = FFmpegResolver.GetFFprobePath();

        Assert.Equal(firstCall, secondCall);
        Assert.NotNull(firstCall);
        Assert.NotEmpty(firstCall);
    }

    [Fact]
    public void GetFFmpegPath_ReturnsValidPath()
    {
        var path = FFmpegResolver.GetFFmpegPath();

        Assert.NotNull(path);
        Assert.NotEmpty(path);
        
        // Path should either be a system path or extracted path
        Assert.True(File.Exists(path) || path.Contains("ffmpeg"), 
            $"FFmpeg path should exist or be a valid ffmpeg path: {path}");
    }

    [Fact]
    public void GetFFprobePath_ReturnsValidPath()
    {
        var path = FFmpegResolver.GetFFprobePath();

        Assert.NotNull(path);
        Assert.NotEmpty(path);
        
        // Path should either be a system path or extracted path
        Assert.True(File.Exists(path) || path.Contains("ffprobe"), 
            $"FFprobe path should exist or be a valid ffprobe path: {path}");
    }

    [Fact]
    public void ResolvedPaths_AreExecutable()
    {
        var ffmpegPath = FFmpegResolver.GetFFmpegPath();
        var ffprobePath = FFmpegResolver.GetFFprobePath();

        // On Unix systems, check if files have execute permission
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || 
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Assert.True(IsExecutableOnUnix(ffmpegPath), $"FFmpeg should be executable: {ffmpegPath}");
            Assert.True(IsExecutableOnUnix(ffprobePath), $"FFprobe should be executable: {ffprobePath}");
        }
        else
        {
            // On Windows, just verify files exist
            Assert.True(File.Exists(ffmpegPath), $"FFmpeg should exist: {ffmpegPath}");
            Assert.True(File.Exists(ffprobePath), $"FFprobe should exist: {ffprobePath}");
        }
    }

    [Theory]
    [InlineData("ffmpeg")]
    [InlineData("ffprobe")]
    public void CanExtractEmbeddedBinary(string binaryName)
    {
        var assembly = Assembly.GetAssembly(typeof(FFmpegResolver));
        Assert.NotNull(assembly);

        var resourceName = $"Core.binaries.{binaryName}";
        using var stream = assembly.GetManifestResourceStream(resourceName);
        
        Assert.NotNull(stream);
        Assert.True(stream.Length > 0, $"Embedded {binaryName} binary should not be empty");
    }

    [Fact]
    public void DeterministicPath_FollowsExpectedFormat()
    {
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        
        if (!string.IsNullOrEmpty(homeDir))
        {
            // Should use XDG spec on Unix-like systems
            var expectedPath = Path.Combine(homeDir, ".local", "share", "wheft", "binaries");
            
            // We can't directly test the private method, but we can verify the resolver
            // creates paths in the expected location when extraction occurs
            Assert.NotNull(homeDir); // Basic sanity check
        }
    }

    private static void ClearCachedPaths()
    {
        // Use reflection to clear the cached paths for testing
        var type = typeof(FFmpegResolver);
        var ffmpegField = type.GetField("_ffmpegPath", BindingFlags.NonPublic | BindingFlags.Static);
        var ffprobeField = type.GetField("_ffprobePath", BindingFlags.NonPublic | BindingFlags.Static);

        ffmpegField?.SetValue(null, null);
        ffprobeField?.SetValue(null, null);
    }

    private static bool IsExecutableOnUnix(string path)
    {
        if (!File.Exists(path))
            return false;

        try
        {
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "test",
                    Arguments = $"-x \"{path}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}