using System;
using System.IO;
using SkiaSharp;

namespace QuickFile.Core;

public static class ImageProcessor
{
    public static string ConvertImage(string inputPath, string targetFormat, int quality = 90, bool overwrite = false)
    {
        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"Input file not found: {inputPath}");
        }

        var format = ParseFormat(targetFormat);
        string extension = GetExtension(targetFormat);

        string dir = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
        string baseName = Path.GetFileNameWithoutExtension(inputPath);
        string outputPath = Path.Combine(dir, $"{baseName}_converted{extension}");

        if (!overwrite && File.Exists(outputPath))
        {
            outputPath = GenerateUniquePath(dir, $"{baseName}_converted", extension);
        }

        return SaveEncodedImage(inputPath, outputPath, format, quality, overwrite);
    }

    public static string CompressImage(string inputPath, int quality = 75, string? targetFormat = null, bool overwrite = false)
    {
        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"Input file not found: {inputPath}");
        }

        string ext = Path.GetExtension(inputPath).TrimStart('.').ToLowerInvariant();
        string formatStr = targetFormat ?? (ext == "png" ? "webp" : ext); // Default to webp if png for meaningful compression, or keep original
        var format = ParseFormat(formatStr);
        string outExt = GetExtension(formatStr);

        string dir = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
        string baseName = Path.GetFileNameWithoutExtension(inputPath);
        string outputPath = Path.Combine(dir, $"{baseName}_compressed{outExt}");

        if (!overwrite && File.Exists(outputPath))
        {
            outputPath = GenerateUniquePath(dir, $"{baseName}_compressed", outExt);
        }

        return SaveEncodedImage(inputPath, outputPath, format, quality, overwrite);
    }

    public static string CompressToTargetSize(string inputPath, long targetBytes, string? preferredFormat = null, bool overwrite = false)
    {
        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"Input file not found: {inputPath}");
        }

        if (targetBytes <= 0)
        {
            throw new ArgumentException("Target size must be greater than zero.", nameof(targetBytes));
        }

        string ext = Path.GetExtension(inputPath).TrimStart('.').ToLowerInvariant();
        string formatStr = preferredFormat ?? (ext == "png" ? "webp" : (ext == "bmp" ? "jpg" : ext));
        var format = ParseFormat(formatStr);
        string outExt = GetExtension(formatStr);

        string dir = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
        string baseName = Path.GetFileNameWithoutExtension(inputPath);
        string outputPath = Path.Combine(dir, $"{baseName}_compressed{outExt}");

        if (!overwrite && File.Exists(outputPath))
        {
            outputPath = GenerateUniquePath(dir, $"{baseName}_compressed", outExt);
        }

        using var inStream = File.OpenRead(inputPath);
        using var codec = SKCodec.Create(inStream);
        if (codec == null) throw new InvalidDataException("Unable to decode source image.");

        using var originalBitmap = SKBitmap.Decode(codec);
        if (originalBitmap == null) throw new InvalidDataException("Unable to decode bitmap pixels.");

        // Binary search for quality (1..100)
        int low = 5;
        int high = 100;
        int bestQuality = 80;
        byte[]? bestData = null;
        SKBitmap currentBitmap = originalBitmap;
        bool createdNewBitmap = false;

        try
        {
            double scale = 1.0;
            for (int attempt = 0; attempt < 5; attempt++) // Max 5 scaling iterations if image is massive
            {
                if (scale < 1.0)
                {
                    int newWidth = Math.Max(16, (int)(originalBitmap.Width * scale));
                    int newHeight = Math.Max(16, (int)(originalBitmap.Height * scale));

                    var scaled = new SKBitmap(newWidth, newHeight);
                    originalBitmap.ScalePixels(scaled, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
                    if (createdNewBitmap) currentBitmap.Dispose();
                    currentBitmap = scaled;
                    createdNewBitmap = true;
                }

                low = 5;
                high = 95;

                while (low <= high)
                {
                    int mid = (low + high) / 2;
                    using var image = SKImage.FromBitmap(currentBitmap);
                    using var data = image.Encode(format, mid);
                    long size = data.Size;

                    if (size <= targetBytes)
                    {
                        bestQuality = mid;
                        bestData = data.ToArray();
                        low = mid + 1; // Try higher quality
                    }
                    else
                    {
                        high = mid - 1; // Quality too large, lower it
                    }
                }

                if (bestData != null)
                {
                    break;
                }

                // If even quality 5 is too large, reduce resolution by 25%
                scale *= 0.75;
            }

            // Fallback if target was ridiculously tiny
            if (bestData == null)
            {
                using var image = SKImage.FromBitmap(currentBitmap);
                using var data = image.Encode(format, 10);
                bestData = data.ToArray();
            }

            string tempPath = Path.Combine(dir, $"{Guid.NewGuid():N}.tmp");
            File.WriteAllBytes(tempPath, bestData);
            File.Move(tempPath, outputPath, overwrite);
            return outputPath;
        }
        finally
        {
            if (createdNewBitmap)
            {
                currentBitmap.Dispose();
            }
        }
    }

    public static string ResizeImage(string inputPath, int targetWidth, int targetHeight, bool overwrite = false)
    {
        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"Input file not found: {inputPath}");
        }

        if (targetWidth <= 0 || targetHeight <= 0)
        {
            throw new ArgumentException("Target dimensions must be positive integers.");
        }

        string ext = Path.GetExtension(inputPath).ToLowerInvariant();
        string formatStr = ext.TrimStart('.');
        var format = ParseFormat(formatStr);

        string dir = Path.GetDirectoryName(inputPath) ?? Environment.CurrentDirectory;
        string baseName = Path.GetFileNameWithoutExtension(inputPath);
        string outputPath = Path.Combine(dir, $"{baseName}_resized{ext}");

        if (!overwrite && File.Exists(outputPath))
        {
            outputPath = GenerateUniquePath(dir, $"{baseName}_resized", ext);
        }

        using var inStream = File.OpenRead(inputPath);
        using var codec = SKCodec.Create(inStream);
        if (codec == null) throw new InvalidDataException("Unable to decode source image.");

        using var original = SKBitmap.Decode(codec);
        if (original == null) throw new InvalidDataException("Unable to decode bitmap pixels.");

        using var resized = new SKBitmap(targetWidth, targetHeight);
        original.ScalePixels(resized, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));

        using var image = SKImage.FromBitmap(resized);
        using var data = image.Encode(format, 95);

        string tempPath = Path.Combine(dir, $"{Guid.NewGuid():N}.tmp");
        using (var outStream = File.Open(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            data.SaveTo(outStream);
        }

        File.Move(tempPath, outputPath, overwrite);
        return outputPath;
    }

    public static ImageFileInfo GetImageInfo(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        var fi = new FileInfo(filePath);
        int width = 0, height = 0;
        string formatName = fi.Extension.TrimStart('.').ToUpperInvariant();

        try
        {
            using var inStream = File.OpenRead(filePath);
            using var codec = SKCodec.Create(inStream);
            if (codec != null)
            {
                width = codec.Info.Width;
                height = codec.Info.Height;
                formatName = codec.EncodedFormat.ToString().ToUpperInvariant();
            }
        }
        catch { }

        return new ImageFileInfo(
            filePath,
            fi.Name,
            formatName,
            fi.Length,
            width,
            height,
            fi.LastWriteTime
        );
    }

    private static string SaveEncodedImage(string inputPath, string outputPath, SKEncodedImageFormat format, int quality, bool overwrite)
    {
        string dir = Path.GetDirectoryName(outputPath) ?? Environment.CurrentDirectory;
        string tempPath = Path.Combine(dir, $"{Guid.NewGuid():N}.tmp");

        try
        {
            using (var inStream = File.OpenRead(inputPath))
            using (var codec = SKCodec.Create(inStream))
            {
                if (codec == null) throw new InvalidDataException("Unable to decode image.");
                using (var bitmap = SKBitmap.Decode(codec))
                {
                    if (bitmap == null) throw new InvalidDataException("Failed to decode image data.");
                    using (var image = SKImage.FromBitmap(bitmap))
                    using (var encodedData = image.Encode(format, Math.Clamp(quality, 1, 100)))
                    using (var outStream = File.Open(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        encodedData.SaveTo(outStream);
                    }
                }
            }

            File.Move(tempPath, outputPath, overwrite);
            return outputPath;
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }
            throw;
        }
    }

    private static SKEncodedImageFormat ParseFormat(string format) => format.ToLowerInvariant() switch
    {
        "png" => SKEncodedImageFormat.Png,
        "jpg" or "jpeg" => SKEncodedImageFormat.Jpeg,
        "webp" => SKEncodedImageFormat.Webp,
        "bmp" => SKEncodedImageFormat.Bmp,
        _ => throw new NotSupportedException($"Target format '{format}' is not supported.")
    };

    private static string GetExtension(string format) => format.ToLowerInvariant() switch
    {
        "jpeg" => ".jpg",
        var s => "." + s
    };

    private static string GenerateUniquePath(string dir, string prefix, string ext)
    {
        int counter = 1;
        string path;
        do
        {
            path = Path.Combine(dir, $"{prefix} ({counter}){ext}");
            counter++;
        } while (File.Exists(path));
        return path;
    }
}
public record ImageFileInfo(
    string FullPath,
    string FileName,
    string Format,
    long SizeBytes,
    int Width,
    int Height,
    DateTime LastModified
)
{
    public string FormattedSize => SizeBytes switch
    {
        < 1024 => $"{SizeBytes} B",
        < 1024 * 1024 => $"{SizeBytes / 1024.0:F1} KB",
        _ => $"{SizeBytes / (1024.0 * 1024.0):F2} MB"
    };

    public string Dimensions => Width > 0 && Height > 0 ? $"{Width} × {Height} px" : "Unknown";
    public string AspectRatio => Width > 0 && Height > 0 ? $"{(double)Width / Height:F2}:1" : "Unknown";
}
