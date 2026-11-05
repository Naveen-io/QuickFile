using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;

namespace QuickFile.Core;

public enum VideoPreset
{
    Smaller,
    Balanced,
    HighQuality
}

public enum VideoResolution
{
    Original,
    P1080,
    P720,
    P480
}

public class VideoConversionOptions
{
    public string TargetFormat { get; set; } = "mp4";
    public VideoPreset Preset { get; set; } = VideoPreset.Balanced;
    public VideoResolution Resolution { get; set; } = VideoResolution.Original;
    public bool ExtractAudioOnly { get; set; } = false;
    public string AudioExtractFormat { get; set; } = "mp3";
    public bool Overwrite { get; set; } = false;
}

public static class VideoProcessor
{
    private static readonly Regex TimeRegex = new(@"time=(\d{2}):(\d{2}):(\d{2}\.\d+)", RegexOptions.Compiled);
    private static readonly Regex SpeedRegex = new(@"speed=\s*([\d\.]+)x", RegexOptions.Compiled);

    public static string ConvertVideo(
        string inputPath,
        string targetFormat,
        Action<double, string>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        var options = new VideoConversionOptions
        {
            TargetFormat = targetFormat,
            Preset = VideoPreset.Balanced,
            Resolution = VideoResolution.Original
        };
        return ConvertVideoWithOptions(inputPath, options, onProgress, cancellationToken);
    }

    public static string ExtractAudio(
        string inputPath,
        string targetFormat = "mp3",
        Action<double, string>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        var options = new VideoConversionOptions
        {
            ExtractAudioOnly = true,
            AudioExtractFormat = targetFormat
        };
        return ConvertVideoWithOptions(inputPath, options, onProgress, cancellationToken);
    }

    public static string ConvertVideoWithOptions(
        string inputPath,
        VideoConversionOptions options,
        Action<double, string>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"Video file not found: {inputPath}");
        }

        string ffmpegPath = FFmpegHelper.GetFFmpegPath();
        var probe = FFmpegHelper.ProbeFile(inputPath);
        double totalDurationSec = probe.Duration.TotalSeconds;

        string targetExt;
        string prefix;

        if (options.ExtractAudioOnly)
        {
            targetExt = AudioProcessor.NormalizeAudioExtension(options.AudioExtractFormat);
            prefix = "extracted_audio";
        }
        else
        {
            targetExt = NormalizeVideoExtension(options.TargetFormat);
            prefix = "converted";
        }

        string dir = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
        string baseName = Path.GetFileNameWithoutExtension(inputPath);
        string outputPath = Path.Combine(dir, $"{baseName}_{prefix}{targetExt}");

        if (!options.Overwrite && File.Exists(outputPath))
        {
            outputPath = FilePathHelper.GenerateUniquePath(dir, $"{baseName}_{prefix}", targetExt);
        }

        string tempPath = Path.Combine(dir, $"{Guid.NewGuid():N}.tmp{targetExt}");

        string args = options.ExtractAudioOnly
            ? BuildExtractAudioArgs(inputPath, tempPath, targetExt)
            : BuildVideoArgs(inputPath, tempPath, targetExt, options);

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

                        string speedStr = "";
                        var speedMatch = SpeedRegex.Match(line);
                        if (speedMatch.Success)
                        {
                            speedStr = $" ({speedMatch.Groups[1].Value}x)";
                        }

                        string actionLabel = options.ExtractAudioOnly ? "Extracting Audio" : $"Encoding {options.TargetFormat.ToUpperInvariant()}";
                        onProgress(percent, $"{actionLabel}: {(int)(percent * 100)}%{speedStr}");
                    }
                }
            }

            process.WaitForExit();
            reg.Dispose();

            if (cancellationToken.IsCancellationRequested)
            {
                CleanupTemp(tempPath);
                throw new OperationCanceledException("Video operation was cancelled.");
            }

            if (process.ExitCode != 0)
            {
                CleanupTemp(tempPath);
                throw new InvalidOperationException($"FFmpeg conversion failed with exit code {process.ExitCode}.");
            }

            if (!File.Exists(tempPath) || new FileInfo(tempPath).Length == 0)
            {
                CleanupTemp(tempPath);
                throw new InvalidOperationException("Output file was not generated or is empty.");
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

    private static string BuildExtractAudioArgs(string inputPath, string tempPath, string audioExt)
    {
        string codec = audioExt switch
        {
            ".mp3" => "-c:a libmp3lame -b:a 192k",
            ".wav" => "-c:a pcm_s16le",
            ".flac" => "-c:a flac",
            ".aac" => "-c:a aac -b:a 192k",
            ".m4a" => "-c:a aac -b:a 192k",
            ".ogg" => "-c:a libvorbis -b:a 192k",
            _ => "-c:a libmp3lame -b:a 192k"
        };

        return $"-y -i \"{inputPath}\" -vn {codec} \"{tempPath}\"";
    }

    private static string BuildVideoArgs(string inputPath, string tempPath, string targetExt, VideoConversionOptions options)
    {
        string videoCodec;
        string audioCodec;
        string crf;
        string speedPreset;

        if (targetExt == ".webm")
        {
            videoCodec = "libvpx-vp9";
            audioCodec = "-c:a libopus -b:a 128k";
            crf = options.Preset switch
            {
                VideoPreset.Smaller => "-crf 36 -b:v 0",
                VideoPreset.HighQuality => "-crf 24 -b:v 0",
                _ => "-crf 30 -b:v 0"
            };
            speedPreset = "-deadline good -cpu-used 2";
        }
        else if (targetExt == ".avi")
        {
            videoCodec = "mpeg4 -q:v 4";
            audioCodec = "-c:a libmp3lame -b:a 160k";
            crf = "";
            speedPreset = "";
        }
        else
        {
            // MP4, MKV, MOV (Standard H.264 + AAC)
            videoCodec = "libx264 -pix_fmt yuv420p";
            audioCodec = options.Preset switch
            {
                VideoPreset.Smaller => "-c:a aac -b:a 96k",
                VideoPreset.HighQuality => "-c:a aac -b:a 256k",
                _ => "-c:a aac -b:a 160k"
            };

            int crfVal = options.Preset switch
            {
                VideoPreset.Smaller => 28,
                VideoPreset.HighQuality => 18,
                _ => 23
            };
            crf = $"-crf {crfVal}";

            string presetName = options.Preset switch
            {
                VideoPreset.Smaller => "fast",
                VideoPreset.HighQuality => "slow",
                _ => "medium"
            };
            speedPreset = $"-preset {presetName}";
        }

        // Resolution scale filter preserving aspect ratio
        string filterArg = options.Resolution switch
        {
            VideoResolution.P1080 => "-vf \"scale='min(1920,iw)':-2\"",
            VideoResolution.P720 => "-vf \"scale='min(1280,iw)':-2\"",
            VideoResolution.P480 => "-vf \"scale='min(854,iw)':-2\"",
            _ => ""
        };

        return $"-y -i \"{inputPath}\" -c:v {videoCodec} {crf} {speedPreset} {filterArg} {audioCodec} \"{tempPath}\"";
    }

    public static string NormalizeVideoExtension(string format)
    {
        string f = format.Trim().TrimStart('.').ToLowerInvariant();
        return f switch
        {
            "mp4" => ".mp4",
            "mkv" => ".mkv",
            "mov" => ".mov",
            "webm" => ".webm",
            "avi" => ".avi",
            "wmv" => ".wmv",
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
