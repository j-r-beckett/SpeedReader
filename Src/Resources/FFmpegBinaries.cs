using System.Diagnostics;

namespace Resources;

public static class FFmpegBinaries
{
    public static string GetFFmpegPath() => GetFFmpegPath(useSystemPath: true);
    public static string GetFFprobePath() => GetFFprobePath(useSystemPath: true);

    public static string GetFFmpegPath(bool useSystemPath) => BinaryResolver.GetBinaryPath(FFmpegBinary.FFmpeg, useSystemPath);
    public static string GetFFprobePath(bool useSystemPath) => BinaryResolver.GetBinaryPath(FFmpegBinary.FFprobe, useSystemPath);
}

public enum FFmpegBinary
{
    FFmpeg,
    FFprobe
}

internal static class BinaryResolver
{
    private static readonly Dictionary<(FFmpegBinary, bool), string> _cachedPaths = new();
    private static readonly Lock _lock = new();

    public static string GetBinaryPath(FFmpegBinary binary, bool useSystemPath)
    {
        var cacheKey = (binary, useSystemPath);

        // Check cache first
        if (_cachedPaths.TryGetValue(cacheKey, out var cachedPath))
            return cachedPath;

        lock (_lock)
        {
            // Double-check after acquiring lock
            if (_cachedPaths.TryGetValue(cacheKey, out cachedPath))
                return cachedPath;

            // Cache miss - resolve the path
            var resolvedPath = ResolveBinary(GetBinaryName(binary), useSystemPath);
            _cachedPaths[cacheKey] = resolvedPath;
            return resolvedPath;
        }
    }

    private static string GetBinaryName(FFmpegBinary binary) => binary switch
    {
        FFmpegBinary.FFmpeg => "ffmpeg",
        FFmpegBinary.FFprobe => "ffprobe",
        _ => throw new ArgumentException($"Unknown binary {binary}")
    };

    private static string ResolveBinary(string binaryName, bool useSystemPath)
    {
        // 1. Try system binary first (if enabled)
        if (useSystemPath)
        {
            var systemPath = FindSystemBinary(binaryName);
            if (systemPath != null)
            {
                return systemPath;
            }
        }

        // 2. Check deterministic location
        var deterministicPath = GetDeterministicPath(binaryName);
        if (File.Exists(deterministicPath) && IsExecutable(deterministicPath))
        {
            return deterministicPath;
        }

        // 3. Extract embedded resource to deterministic location
        try
        {
            ExtractEmbeddedBinary(binaryName, deterministicPath);
            if (File.Exists(deterministicPath) && IsExecutable(deterministicPath))
            {
                return deterministicPath;
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to extract embedded {binaryName} binary: {ex.Message}", ex);
        }

        // 4. Fail gracefully with clear error message
        var searchedMethods = useSystemPath ? "system PATH, deterministic location, and embedded resource" : "deterministic location and embedded resource";
        throw new FileNotFoundException(
            $"Could not locate {binaryName} binary. " +
            $"Searched: {searchedMethods}. " +
            $"Deterministic path attempted: {deterministicPath}");
    }

    private static string? FindSystemBinary(string binaryName)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = binaryName,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();

            if (process.ExitCode == 0 && !string.IsNullOrEmpty(output) && File.Exists(output))
            {
                return output;
            }
        }
        catch
        {
            // Ignore errors when trying to find system binary
        }

        return null;
    }

    private static string GetDeterministicPath(string binaryName)
    {
        // Follow XDG Base Directory Specification
        // 1. Check XDG_DATA_HOME environment variable
        var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (!string.IsNullOrEmpty(xdgDataHome) && HasWriteAccess(xdgDataHome))
        {
            return Path.Combine(xdgDataHome, "speedreader", "binaries", binaryName);
        }

        // 2. Fallback to ~/.local/share (XDG default)
        var homeDir = Environment.GetEnvironmentVariable("HOME");
        if (!string.IsNullOrEmpty(homeDir))
        {
            var localShareDir = Path.Combine(homeDir, ".local", "share");
            if (HasWriteAccess(localShareDir))
            {
                return Path.Combine(localShareDir, "speedreader", "binaries", binaryName);
            }
        }

        // 3. Final fallback to /tmp for containers/edge cases
        return Path.Combine("/tmp", "speedreader", "binaries", binaryName);
    }

    private static bool HasWriteAccess(string directoryPath)
    {
        try
        {
            if (!Directory.Exists(directoryPath)) return false;

            var testFile = Path.Combine(directoryPath, $".test_{Environment.ProcessId}");
            File.WriteAllText(testFile, "");
            File.Delete(testFile);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void ExtractEmbeddedBinary(string binaryName, string targetPath)
    {
        var resourceName = $"binaries.{binaryName}";
        var data = Resource.GetBytes(resourceName);

        // Ensure target directory exists
        var targetDir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        // Extract binary to target path
        File.WriteAllBytes(targetPath, data);

        // Make executable on Unix systems
        if (Environment.OSVersion.Platform == PlatformID.Unix)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "chmod",
                    Arguments = $"+x \"{targetPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.WaitForExit();
        }
    }

    private static bool IsExecutable(string path)
    {
        try
        {
            var fileInfo = new FileInfo(path);
            if (!fileInfo.Exists)
                return false;

            // On Unix, check if file has execute permission
            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
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

            // On other platforms, just check if file exists
            return true;
        }
        catch
        {
            return false;
        }
    }
}
