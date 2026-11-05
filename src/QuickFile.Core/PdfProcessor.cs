using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PDFtoImage;
using SkiaSharp;

namespace QuickFile.Core;

public record PdfFileInfo(
    string FullPath,
    string FileName,
    long SizeBytes,
    int PageCount,
    string Title,
    DateTime LastModified
)
{
    public string FormattedSize => FilePathHelper.FormatFileSize(SizeBytes);
}

[SupportedOSPlatform("windows")]
public static class PdfProcessor
{
    static PdfProcessor()
    {
        try
        {
            PdfSharpLicense.SetAgplLicense();
        }
        catch { }
    }

    public static PdfFileInfo GetPdfInfo(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"PDF file not found: {filePath}");
        }

        var fi = new FileInfo(filePath);
        int pageCount = 0;
        string title = "";

        try
        {
            using var doc = PdfReader.Open(filePath, PdfDocumentOpenMode.Import);
            pageCount = doc.PageCount;
            title = doc.Info.Title ?? "";
        }
        catch
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(filePath);
                pageCount = Conversion.GetPageCount(bytes);
            }
            catch { }
        }

        return new PdfFileInfo(filePath, fi.Name, fi.Length, pageCount, title, fi.LastWriteTime);
    }

    public static string ImagesToPdf(IEnumerable<string> imagePaths, string? outputPath = null, bool overwrite = false)
    {
        var images = imagePaths.Where(File.Exists).ToList();
        if (images.Count == 0)
        {
            throw new ArgumentException("No valid images provided for PDF conversion.");
        }

        string firstImg = images[0];
        string dir = Path.GetDirectoryName(firstImg) ?? Environment.CurrentDirectory;
        string baseName = Path.GetFileNameWithoutExtension(firstImg);

        if (string.IsNullOrEmpty(outputPath))
        {
            string outName = images.Count > 1 ? $"{baseName}_combined" : $"{baseName}";
            outputPath = Path.Combine(dir, $"{outName}.pdf");
            if (!overwrite && File.Exists(outputPath))
            {
                outputPath = FilePathHelper.GenerateUniquePath(dir, outName, ".pdf");
            }
        }

        string tempPath = Path.Combine(dir, $"{Guid.NewGuid():N}.tmp.pdf");

        try
        {
            using (var doc = SKDocument.CreatePdf(tempPath))
            {
                foreach (var imgPath in images)
                {
                    using var inStream = File.OpenRead(imgPath);
                    using var bmp = SKBitmap.Decode(inStream);
                    if (bmp == null) continue;

                    using var canvas = doc.BeginPage(bmp.Width, bmp.Height);
                    canvas.DrawBitmap(bmp, 0, 0, SKSamplingOptions.Default, null);
                    doc.EndPage();
                }
                doc.Close();
            }

            File.Move(tempPath, outputPath, overwrite);
            return outputPath;
        }
        catch
        {
            if (File.Exists(tempPath)) try { File.Delete(tempPath); } catch { }
            throw;
        }
    }

    public static List<string> PdfToImages(
        string pdfPath,
        string targetFormat = "png",
        int dpi = 150,
        Action<double, string>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(pdfPath))
        {
            throw new FileNotFoundException($"PDF file not found: {pdfPath}");
        }

        string dir = Path.GetDirectoryName(pdfPath) ?? Environment.CurrentDirectory;
        string baseName = Path.GetFileNameWithoutExtension(pdfPath);
        string ext = targetFormat.TrimStart('.').ToLowerInvariant();

        byte[] pdfBytes = File.ReadAllBytes(pdfPath);
        int pageCount = Conversion.GetPageCount(pdfBytes);
        if (pageCount <= 0)
        {
            throw new InvalidDataException("PDF document has 0 pages.");
        }

        var outputFiles = new List<string>();

        for (int i = 0; i < pageCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var bitmap = Conversion.ToImage(pdfBytes, page: i, options: new RenderOptions { Dpi = dpi });
            if (bitmap == null) continue;

            string outFileName = pageCount == 1 ? $"{baseName}.{ext}" : $"{baseName}_page_{i + 1}.{ext}";
            string outputPath = Path.Combine(dir, outFileName);
            if (File.Exists(outputPath))
            {
                outputPath = FilePathHelper.GenerateUniquePath(dir, pageCount == 1 ? baseName : $"{baseName}_page_{i + 1}", $".{ext}");
            }

            var skFormat = ext switch
            {
                "jpg" or "jpeg" => SKEncodedImageFormat.Jpeg,
                "webp" => SKEncodedImageFormat.Webp,
                _ => SKEncodedImageFormat.Png
            };

            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(skFormat, 92);
            using var outStream = File.Open(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
            data.SaveTo(outStream);

            outputFiles.Add(outputPath);

            double progress = (double)(i + 1) / pageCount;
            onProgress?.Invoke(progress, $"Rendering page {i + 1} of {pageCount} ({(int)(progress * 100)}%)");
        }

        return outputFiles;
    }

    public static string MergePdfs(IEnumerable<string> pdfPaths, string? outputPath = null, bool overwrite = false)
    {
        var files = pdfPaths.Where(File.Exists).ToList();
        if (files.Count < 2)
        {
            throw new ArgumentException("At least two valid PDF files are required for merging.");
        }

        string first = files[0];
        string dir = Path.GetDirectoryName(first) ?? Environment.CurrentDirectory;
        string baseName = Path.GetFileNameWithoutExtension(first);

        if (string.IsNullOrEmpty(outputPath))
        {
            outputPath = Path.Combine(dir, $"{baseName}_merged.pdf");
            if (!overwrite && File.Exists(outputPath))
            {
                outputPath = FilePathHelper.GenerateUniquePath(dir, $"{baseName}_merged", ".pdf");
            }
        }

        string tempPath = Path.Combine(dir, $"{Guid.NewGuid():N}.tmp.pdf");

        try
        {
            using var outDoc = new PdfDocument();
            outDoc.Info.Creator = "QuickFile";

            foreach (var file in files)
            {
                using var inDoc = PdfReader.Open(file, PdfDocumentOpenMode.Import);
                for (int i = 0; i < inDoc.PageCount; i++)
                {
                    outDoc.AddPage(inDoc.Pages[i]);
                }
            }

            outDoc.Save(tempPath);
            File.Move(tempPath, outputPath, overwrite);
            return outputPath;
        }
        catch
        {
            if (File.Exists(tempPath)) try { File.Delete(tempPath); } catch { }
            throw;
        }
    }

    public static List<string> SplitPdf(
        string pdfPath,
        Action<double, string>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(pdfPath))
        {
            throw new FileNotFoundException($"PDF file not found: {pdfPath}");
        }

        string dir = Path.GetDirectoryName(pdfPath) ?? Environment.CurrentDirectory;
        string baseName = Path.GetFileNameWithoutExtension(pdfPath);

        using var inDoc = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Import);
        int pageCount = inDoc.PageCount;
        var outputFiles = new List<string>();

        for (int i = 0; i < pageCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var outDoc = new PdfDocument();
            outDoc.AddPage(inDoc.Pages[i]);

            string outName = $"{baseName}_page_{i + 1}";
            string outPath = Path.Combine(dir, $"{outName}.pdf");
            if (File.Exists(outPath))
            {
                outPath = FilePathHelper.GenerateUniquePath(dir, outName, ".pdf");
            }

            outDoc.Save(outPath);
            outputFiles.Add(outPath);

            double progress = (double)(i + 1) / pageCount;
            onProgress?.Invoke(progress, $"Splitting page {i + 1} of {pageCount} ({(int)(progress * 100)}%)");
        }

        return outputFiles;
    }

    public static string ExtractPages(string pdfPath, string pageRangeSpec, string? outputPath = null, bool overwrite = false)
    {
        if (!File.Exists(pdfPath))
        {
            throw new FileNotFoundException($"PDF file not found: {pdfPath}");
        }

        string dir = Path.GetDirectoryName(pdfPath) ?? Environment.CurrentDirectory;
        string baseName = Path.GetFileNameWithoutExtension(pdfPath);

        if (string.IsNullOrEmpty(outputPath))
        {
            outputPath = Path.Combine(dir, $"{baseName}_extracted.pdf");
            if (!overwrite && File.Exists(outputPath))
            {
                outputPath = FilePathHelper.GenerateUniquePath(dir, $"{baseName}_extracted", ".pdf");
            }
        }

        using var inDoc = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Import);
        var pageIndices = ParsePageRange(pageRangeSpec, inDoc.PageCount);

        if (pageIndices.Count == 0)
        {
            throw new ArgumentException("No valid pages found in specified page range.");
        }

        string tempPath = Path.Combine(dir, $"{Guid.NewGuid():N}.tmp.pdf");

        try
        {
            using var outDoc = new PdfDocument();
            foreach (int pageIndex in pageIndices)
            {
                outDoc.AddPage(inDoc.Pages[pageIndex]);
            }

            outDoc.Save(tempPath);
            File.Move(tempPath, outputPath, overwrite);
            return outputPath;
        }
        catch
        {
            if (File.Exists(tempPath)) try { File.Delete(tempPath); } catch { }
            throw;
        }
    }

    public static string RotatePages(string pdfPath, int rotationDegrees, string? outputPath = null, bool overwrite = false)
    {
        if (!File.Exists(pdfPath))
        {
            throw new FileNotFoundException($"PDF file not found: {pdfPath}");
        }

        string dir = Path.GetDirectoryName(pdfPath) ?? Environment.CurrentDirectory;
        string baseName = Path.GetFileNameWithoutExtension(pdfPath);

        if (string.IsNullOrEmpty(outputPath))
        {
            outputPath = Path.Combine(dir, $"{baseName}_rotated.pdf");
            if (!overwrite && File.Exists(outputPath))
            {
                outputPath = FilePathHelper.GenerateUniquePath(dir, $"{baseName}_rotated", ".pdf");
            }
        }

        string tempPath = Path.Combine(dir, $"{Guid.NewGuid():N}.tmp.pdf");

        try
        {
            using var doc = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Modify);
            foreach (var page in doc.Pages)
            {
                page.Rotate = (page.Rotate + rotationDegrees) % 360;
            }

            doc.Save(tempPath);
            File.Move(tempPath, outputPath, overwrite);
            return outputPath;
        }
        catch
        {
            if (File.Exists(tempPath)) try { File.Delete(tempPath); } catch { }
            throw;
        }
    }

    private static List<int> ParsePageRange(string spec, int maxPages)
    {
        var result = new HashSet<int>();
        var parts = spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            if (part.Contains('-'))
            {
                var sub = part.Split('-');
                if (sub.Length == 2 && int.TryParse(sub[0], out int start) && int.TryParse(sub[1], out int end))
                {
                    for (int p = Math.Min(start, end); p <= Math.Max(start, end); p++)
                    {
                        if (p >= 1 && p <= maxPages) result.Add(p - 1);
                    }
                }
            }
            else if (int.TryParse(part, out int pageNum))
            {
                if (pageNum >= 1 && pageNum <= maxPages) result.Add(pageNum - 1);
            }
        }

        return result.OrderBy(x => x).ToList();
    }
}
