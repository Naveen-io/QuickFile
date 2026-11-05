using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace QuickFile.Core;

public enum AudioPreset
{
    Smaller,
    Balanced,
    HighQuality
}

public class AudioConversionOptions
{
    public string TargetFormat { get; set; } = "mp3";
    public AudioPreset? Preset { get; set; } = AudioPreset.Balanced;
    public int? BitrateKbps { get; set; }
    public int? SampleRateHz { get; set; }
    public int? Channels { get; set; }
    public bool Overwrite { get; set; } = false;
}

public static class AudioProcessor
{
    private static readonly Regex TimeRegex = new(@"time=(\d{2}):(\d{2}):(\d{2}\.\d+)", RegexOptions.Compiled);

    public static string ConvertAudio(
        string inputPath,
        string targetFormat,
        Action<double, string>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        var options = new AudioConversionOptions
        {
            TargetFormat = targetFormat,
            Preset = AudioPreset.Balanced
        };
        return ConvertAudioWithOptions(inputPath, options, onProgress, cancellationToken);
    }

    public static string ConvertAudioWithOptions(
        string inputPath,
        AudioConversionOptions options,
        Action<double, string>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"Audio file not found: {inputPath}");
        }

        string ffmpegPath = FFmpegHelper.GetFFmpegPath();
        var probe = FFmpegHelper.ProbeFile(inputPath);
        double totalDurationSec = probe.Duration.TotalSeconds;

        string targetExt = NormalizeAudioExtension(options.TargetFormat);
        string dir = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
        string baseName = Path.GetFileNameWithoutExtension(inputPath);
        string outputPath = Path.Combine(dir, $"{baseName}_converted{targetExt}");

        if (!options.Overwrite && File.Exists(outputPath))
        {
            outputPath = FilePathHelper.GenerateUniquePath(dir, $"{baseName}_converted", targetExt);
        }

        string tempPath = Path.Combine(dir, $"{Guid.NewGuid():N}.tmp{targetExt}");

        // Build FFmpeg arguments
        var args = BuildAudioArguments(inputPath, tempPath, targetExt, options);

        var psi = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = false
        };

        using var process = new Process { StartInfo = psi };

        try
        {
            process.Start();

            var reg = cancellationToken.Register(() =>
            {
                try { if (!process.HasExited) process.Kill(true); } catch { }
            });

            string? line;
            while ((line = process.StandardError.ReadLine()) != null)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (totalDurationSec > 0 && onProgress != null)
                {
                    var match = TimeRegex.Match(line);
                    if (match.Success)
                    {
                        int h = int.Parse(match.Groups[1].Value);
                        int m = int.Parse(match.Groups[2].Value);
                        double s = double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
                        double currentSec = (h * 3600) + (m * 60) + s;
                        double percent = Math.Clamp(currentSec / totalDurationSec, 0.0, 1.0);
                        onProgress(percent, $"Encoding {options.TargetFormat.ToUpperInvariant()}: {(int)(percent * 100)}%");
                    }
                }
            }

            process.WaitForExit();
            reg.Dispose();

            if (cancellationToken.IsCancellationRequested)
            {
                CleanupTemp(tempPath);
                throw new OperationCanceledException("Audio conversion was cancelled.");
            }

            if (process.ExitCode != 0)
            {
                CleanupTemp(tempPath);
                throw new InvalidOperationException($"FFmpeg conversion failed with exit code {process.ExitCode}.");
            }

            if (!File.Exists(tempPath) || new FileInfo(tempPath).Length == 0)
            {
                CleanupTemp(tempPath);
                throw new InvalidOperationException("Output audio file was not generated or is empty.");
            }

            File.Move(tempPath, outputPath, options.Overwrite);
            onProgress?.Invoke(1.0, "Completed");
            return outputPath;
        }
        catch
        {
            CleanupTemp(tempPath);
            throw;
        }
    }

    private static string BuildAudioArguments(string inputPath, string tempPath, string targetExt, AudioConversionOptions options)
    {
        // Codec selection
        string codecArg = targetExt switch
        {
            ".mp3" => "-c:a libmp3lame",
            ".wav" => "-c:a pcm_s16le",
            ".flac" => "-c:a flac",
            ".aac" => "-c:a aac",
            ".m4a" => "-c:a aac",
            ".ogg" => "-c:a libvorbis",
            _ => "-c:a copy"
        };

        // Bitrate
        string bitrateArg = "";
        if (targetExt is not ".wav" and not ".flac")
        {
            if (options.BitrateKbps.HasValue)
            {
                bitrateArg = $"-b:a {options.BitrateKbps.Value}k";
            }
            else
            {
                int kbps = options.Preset switch
                {
                    AudioPreset.Smaller => 128,
                    AudioPreset.HighQuality => 320,
                    _ => 192
                };
                bitrateArg = $"-b:a {kbps}k";
            }
        }

        // Sample rate
        string sampleRateArg = options.SampleRateHz.HasValue ? $"-ar {options.SampleRateHz.Value}" : "";

        // Channels
        string channelsArg = options.Channels.HasValue ? $"-ac {options.Channels.Value}" : "";

        return $"-y -i \"{inputPath}\" -vn {codecArg} {bitrateArg} {sampleRateArg} {channelsArg} \"{tempPath}\"";
    }

    public static string NormalizeAudioExtension(string format)
    {
        string f = format.Trim().TrimStart('.').ToLowerInvariant();
        return f switch
        {
            "mp3" => ".mp3",
            "wav" => ".wav",
            "flac" => ".flac",
            "aac" => ".aac",
            "m4a" => ".m4a",
            "ogg" => ".ogg",
            "wma" => ".wma",
            _ => $".{f}"
        };
    }

    private static void CleanupTemp(string path)
    {
        if (File.Exists(path))
        {
            try { File.Delete(path); } catch { }
        }
    }
}
