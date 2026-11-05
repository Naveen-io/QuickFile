using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace QuickFile.Core;

public static class FFmpegHelper
{
    private static string? _cachedFfmpegPath;
    private static string? _cachedFfprobePath;

    public static string GetFFmpegPath()
    {
        if (_cachedFfmpegPath != null && File.Exists(_cachedFfmpegPath))
        {
            return _cachedFfmpegPath;
        }

        _cachedFfmpegPath = FindExecutable("ffmpeg.exe");
        if (string.IsNullOrEmpty(_cachedFfmpegPath))
        {
            throw new FileNotFoundException(
                "FFmpeg executable not found. Please ensure FFmpeg is installed or in your system PATH.");
        }

        return _cachedFfmpegPath;
    }

    public static string GetFFprobePath()
    {
        if (_cachedFfprobePath != null && File.Exists(_cachedFfprobePath))
        {
            return _cachedFfprobePath;
        }

        _cachedFfprobePath = FindExecutable("ffprobe.exe");
        if (string.IsNullOrEmpty(_cachedFfprobePath))
        {
            throw new FileNotFoundException(
                "FFprobe executable not found. Please ensure FFmpeg/FFprobe is installed or in your system PATH.");
        }

        return _cachedFfprobePath;
    }

    public static bool IsFFmpegAvailable()
    {
        try
        {
            return !string.IsNullOrEmpty(GetFFmpegPath());
        }
        catch
        {
            return false;
        }
    }

    private static string? FindExecutable(string exeName)
    {
        // 1. Check local app directory
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string localCandidate = Path.Combine(baseDir, exeName);
        if (File.Exists(localCandidate)) return localCandidate;

        // 2. Check system PATH
        string? pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathEnv))
        {
            foreach (var segment in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    string candidate = Path.Combine(segment.Trim(), exeName);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                catch { }
            }
        }

        // 3. Check WinGet Gyan.FFmpeg package location in LocalAppData
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string wingetPackages = Path.Combine(localAppData, "Microsoft", "WinGet", "Packages");
        if (Directory.Exists(wingetPackages))
        {
            try
            {
                var matches = Directory.GetFiles(wingetPackages, exeName, SearchOption.AllDirectories);
                if (matches.Length > 0)
                {
                    return matches[0];
                }
            }
            catch { }
        }

        // 4. Check Program Files
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string pfCandidate = Path.Combine(programFiles, "ffmpeg", "bin", exeName);
        if (File.Exists(pfCandidate)) return pfCandidate;

        return null;
    }

    public static MediaProbeInfo ProbeFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        var fi = new FileInfo(filePath);
        string ffprobe = GetFFprobePath();

        // Run ffprobe in JSON mode
        var psi = new ProcessStartInfo
        {
            FileName = ffprobe,
            Arguments = $"-v quiet -print_format json -show_format -show_streams \"{filePath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to launch ffprobe process.");
        string jsonOutput = process.StandardOutput.ReadToEnd();
        process.WaitForExit(8000);

        TimeSpan duration = TimeSpan.Zero;
        long bitrate = 0;
        int width = 0;
        int height = 0;
        string videoCodec = "";
        string audioCodec = "";
        int audioChannels = 0;
        int sampleRate = 0;
        string containerFormat = fi.Extension.TrimStart('.').ToUpperInvariant();

        try
        {
            using var doc = JsonDocument.Parse(jsonOutput);
            var root = doc.RootElement;

            if (root.TryGetProperty("format", out var formatElem))
            {
                if (formatElem.TryGetProperty("duration", out var durElem) &&
                    double.TryParse(durElem.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double durSec))
                {
                    duration = TimeSpan.FromSeconds(durSec);
                }

                if (formatElem.TryGetProperty("bit_rate", out var brElem) &&
                    long.TryParse(brElem.GetString(), out long br))
                {
                    bitrate = br;
                }

                if (formatElem.TryGetProperty("format_name", out var fnElem))
                {
                    containerFormat = fnElem.GetString()?.Split(',')[0].ToUpperInvariant() ?? containerFormat;
                }
            }

            if (root.TryGetProperty("streams", out var streamsElem) && streamsElem.ValueKind == JsonValueKind.Array)
            {
                foreach (var stream in streamsElem.EnumerateArray())
                {
                    string codecType = stream.TryGetProperty("codec_type", out var ct) ? ct.GetString() ?? "" : "";
                    if (codecType == "video" && width == 0)
                    {
                        if (stream.TryGetProperty("width", out var w)) width = w.GetInt32();
                        if (stream.TryGetProperty("height", out var h)) height = h.GetInt32();
                        if (stream.TryGetProperty("codec_name", out var cn)) videoCodec = cn.GetString()?.ToUpperInvariant() ?? "";
                    }
                    else if (codecType == "audio" && audioCodec == "")
                    {
                        if (stream.TryGetProperty("codec_name", out var acn)) audioCodec = acn.GetString()?.ToUpperInvariant() ?? "";
                        if (stream.TryGetProperty("channels", out var ch)) audioChannels = ch.GetInt32();
                        if (stream.TryGetProperty("sample_rate", out var sr) && int.TryParse(sr.GetString(), out int srv)) sampleRate = srv;
                    }
                }
            }
        }
        catch
        {
            // Fallback to basic file info if probe parsing fails
        }

        return new MediaProbeInfo(
            filePath,
            fi.Name,
            containerFormat,
            fi.Length,
            duration,
            bitrate,
            width,
            height,
            videoCodec,
            audioCodec,
            audioChannels,
            sampleRate,
            fi.LastWriteTime
        );
    }
}

public record MediaProbeInfo(
    string FullPath,
    string FileName,
    string ContainerFormat,
    long SizeBytes,
    TimeSpan Duration,
    long Bitrate,
    int Width,
    int Height,
    string VideoCodec,
    string AudioCodec,
    int AudioChannels,
    int SampleRate,
    DateTime LastModified
)
{
    public string FormattedSize => SizeBytes switch
    {
        < 1024 => $"{SizeBytes} B",
        < 1024 * 1024 => $"{SizeBytes / 1024.0:F1} KB",
        _ => $"{SizeBytes / (1024.0 * 1024.0):F2} MB"
    };

    public string FormattedDuration => Duration.TotalSeconds < 1
        ? "< 1 sec"
        : (Duration.TotalHours >= 1 ? Duration.ToString(@"hh\:mm\:ss") : Duration.ToString(@"mm\:ss"));

    public string Resolution => Width > 0 && Height > 0 ? $"{Width} × {Height} px" : "N/A";
    public string FormattedBitrate => Bitrate > 0 ? $"{Bitrate / 1000} kbps" : "N/A";
    public string FormattedSampleRate => SampleRate > 0 ? $"{SampleRate / 1000.0:F1} kHz" : "N/A";
    public string ChannelDescription => AudioChannels switch
    {
        1 => "Mono (1ch)",
        2 => "Stereo (2ch)",
        6 => "5.1 Surround",
        8 => "7.1 Surround",
        > 0 => $"{AudioChannels} channels",
        _ => "None"
    };
}
