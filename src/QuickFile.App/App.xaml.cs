using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using QuickFile.Core;

namespace QuickFile.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        string[] args = e.Args;
        if (args == null || args.Length == 0)
        {
            var win = new MainWindow();
            win.Show();
            return;
        }

        string exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "";
        bool silent = args.Contains("--silent", StringComparer.OrdinalIgnoreCase);

        if (args.Contains("--register", StringComparer.OrdinalIgnoreCase))
        {
            ShellManager.Register(exePath);
            if (!silent)
            {
                var toast = new ToastNotification("QuickFile Registered", "Context menus active on direct right-click!");
                toast.Closed += (s, ev) => Shutdown(0);
                toast.Show();
            }
            else
            {
                Shutdown(0);
            }
            return;
        }

        if (args.Contains("--unregister", StringComparer.OrdinalIgnoreCase))
        {
            ShellManager.Unregister();
            if (!silent)
            {
                var toast = new ToastNotification("QuickFile Unregistered", "Context menus removed from File Explorer.");
                toast.Closed += (s, ev) => Shutdown(0);
                toast.Show();
            }
            else
            {
                Shutdown(0);
            }
            return;
        }

        if (args.Contains("--settings", StringComparer.OrdinalIgnoreCase))
        {
            var win = new MainWindow();
            win.Show();
            return;
        }

        // Parse command line arguments
        string action = "";
        string format = "";
        string preset = "";
        string resolution = "";
        string angle = "90";
        var fileTokens = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].Equals("--action", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                action = args[++i];
            }
            else if (args[i].Equals("--format", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                format = args[++i];
            }
            else if (args[i].Equals("--preset", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                preset = args[++i];
            }
            else if (args[i].Equals("--res", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                resolution = args[++i];
            }
            else if (args[i].Equals("--angle", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                angle = args[++i];
            }
            else if (!args[i].StartsWith("--"))
            {
                fileTokens.Add(args[i]);
            }
        }

        // Reconstruct target file paths (handling spaces)
        var existingFiles = ParseExistingFiles(fileTokens);

        if (existingFiles.Count == 0)
        {
            var toastErr = new ToastNotification("File Not Found", "The selected file could not be located.");
            toastErr.Closed += (s, ev) => Shutdown(1);
            toastErr.Show();
            return;
        }

        string targetFile = existingFiles[0];

        try
        {
            DispatchAction(action, format, preset, resolution, angle, targetFile, existingFiles);
        }
        catch (Exception ex)
        {
            var toastEx = new ToastNotification("QuickFile Error", ex.Message);
            toastEx.Closed += (s, ev) => Shutdown(1);
            toastEx.Show();
        }
    }

    private void DispatchAction(
        string action,
        string format,
        string preset,
        string resolution,
        string angle,
        string targetFile,
        List<string> allFiles)
    {
        switch (action.ToLowerInvariant())
        {
            case "copy-path":
                Clipboard.SetText(targetFile);
                ShowToastAndExit("Path Copied to Clipboard", Path.GetFileName(targetFile));
                break;

            case "convert":
                // Direct image conversion
                if (string.IsNullOrEmpty(format)) format = "png";
                string imgOut = ImageProcessor.ConvertImage(targetFile, format);
                var fiImg = new FileInfo(imgOut);
                ShowToastAndExit($"Converted to {format.ToUpperInvariant()} ({FilePathHelper.FormatFileSize(fiImg.Length)})", fiImg.Name, imgOut);
                break;

            case "compress":
                string compOut = ImageProcessor.CompressImage(targetFile);
                var fiComp = new FileInfo(compOut);
                ShowToastAndExit($"Compressed Image ({FilePathHelper.FormatFileSize(fiComp.Length)})", fiComp.Name, compOut);
                break;

            case "img-to-pdf":
                // If multiple images passed, combine them; otherwise convert single image to PDF
                string pdfFromImg = PdfProcessor.ImagesToPdf(allFiles);
                var fiPdfImg = new FileInfo(pdfFromImg);
                ShowToastAndExit($"Created PDF ({FilePathHelper.FormatFileSize(fiPdfImg.Length)})", fiPdfImg.Name, pdfFromImg);
                break;

            case "audio-convert":
                ExecuteAudioConvert(targetFile, format, preset);
                break;

            case "video-convert":
                ExecuteVideoConvert(targetFile, format, preset, resolution);
                break;

            case "extract-audio":
                ExecuteExtractAudio(targetFile, format);
                break;

            case "pdf-to-images":
                ExecutePdfToImages(targetFile, format);
                break;

            case "pdf-split":
                ExecutePdfSplit(targetFile);
                break;

            case "pdf-rotate":
                int rotDeg = int.TryParse(angle, out int a) ? a : 90;
                string rotOut = PdfProcessor.RotatePages(targetFile, rotDeg);
                ShowToastAndExit($"Rotated PDF {rotDeg}°", Path.GetFileName(rotOut), rotOut);
                break;

            case "compress-target":
            case "resize":
            case "resize-custom":
            case "audio-options":
            case "video-options":
            case "pdf-tools":
            case "info":
                var miniWin = new MiniActionWindow(action, targetFile);
                miniWin.Closed += (s, ev) => Shutdown(0);
                miniWin.Show();
                break;

            default:
                var toastDef = new ToastNotification("QuickFile", $"Action '{action}' is not supported.");
                toastDef.Closed += (s, ev) => Shutdown(1);
                toastDef.Show();
                break;
        }
    }

    private void ExecuteAudioConvert(string targetFile, string format, string preset)
    {
        string fmt = string.IsNullOrEmpty(format) ? "mp3" : format;
        var audioPreset = preset.ToLowerInvariant() switch
        {
            "smaller" => AudioPreset.Smaller,
            "highquality" => AudioPreset.HighQuality,
            _ => AudioPreset.Balanced
        };

        var cts = new CancellationTokenSource();
        var progWin = new ProgressWindow($"Converting to {fmt.ToUpperInvariant()}", targetFile, cts);
        progWin.Show();

        Task.Run(() =>
        {
            try
            {
                var opts = new AudioConversionOptions
                {
                    TargetFormat = fmt,
                    Preset = audioPreset
                };

                string outPath = AudioProcessor.ConvertAudioWithOptions(targetFile, opts, (p, status) =>
                {
                    progWin.ReportProgress(p, status);
                }, cts.Token);

                Dispatcher.Invoke(() =>
                {
                    progWin.Close();
                    var fi = new FileInfo(outPath);
                    ShowToastAndExit($"Converted to {fmt.ToUpperInvariant()} ({FilePathHelper.FormatFileSize(fi.Length)})", fi.Name, outPath);
                });
            }
            catch (OperationCanceledException)
            {
                Dispatcher.Invoke(() =>
                {
                    progWin.Close();
                    Shutdown(0);
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    progWin.Close();
                    var toastErr = new ToastNotification("Audio Conversion Failed", ex.Message);
                    toastErr.Closed += (s, ev) => Shutdown(1);
                    toastErr.Show();
                });
            }
        });
    }

    private void ExecuteVideoConvert(string targetFile, string format, string preset, string resolution)
    {
        string fmt = string.IsNullOrEmpty(format) ? "mp4" : format;
        var videoPreset = preset.ToLowerInvariant() switch
        {
            "smaller" => VideoPreset.Smaller,
            "highquality" => VideoPreset.HighQuality,
            _ => VideoPreset.Balanced
        };

        var videoRes = resolution switch
        {
            "1080" => VideoResolution.P1080,
            "720" => VideoResolution.P720,
            "480" => VideoResolution.P480,
            _ => VideoResolution.Original
        };

        var cts = new CancellationTokenSource();
        var progWin = new ProgressWindow($"Transcoding to {fmt.ToUpperInvariant()}", targetFile, cts);
        progWin.Show();

        Task.Run(() =>
        {
            try
            {
                var opts = new VideoConversionOptions
                {
                    TargetFormat = fmt,
                    Preset = videoPreset,
                    Resolution = videoRes
                };

                string outPath = VideoProcessor.ConvertVideoWithOptions(targetFile, opts, (p, status) =>
                {
                    progWin.ReportProgress(p, status);
                }, cts.Token);

                Dispatcher.Invoke(() =>
                {
                    progWin.Close();
                    var fi = new FileInfo(outPath);
                    ShowToastAndExit($"Converted to {fmt.ToUpperInvariant()} ({FilePathHelper.FormatFileSize(fi.Length)})", fi.Name, outPath);
                });
            }
            catch (OperationCanceledException)
            {
                Dispatcher.Invoke(() =>
                {
                    progWin.Close();
                    Shutdown(0);
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    progWin.Close();
                    var toastErr = new ToastNotification("Video Conversion Failed", ex.Message);
                    toastErr.Closed += (s, ev) => Shutdown(1);
                    toastErr.Show();
                });
            }
        });
    }

    private void ExecuteExtractAudio(string targetFile, string format)
    {
        string fmt = string.IsNullOrEmpty(format) ? "mp3" : format;
        var cts = new CancellationTokenSource();
        var progWin = new ProgressWindow($"Extracting {fmt.ToUpperInvariant()} Audio", targetFile, cts);
        progWin.Show();

        Task.Run(() =>
        {
            try
            {
                string outPath = VideoProcessor.ExtractAudio(targetFile, fmt, (p, status) =>
                {
                    progWin.ReportProgress(p, status);
                }, cts.Token);

                Dispatcher.Invoke(() =>
                {
                    progWin.Close();
                    var fi = new FileInfo(outPath);
                    ShowToastAndExit($"Audio Extracted ({FilePathHelper.FormatFileSize(fi.Length)})", fi.Name, outPath);
                });
            }
            catch (OperationCanceledException)
            {
                Dispatcher.Invoke(() =>
                {
                    progWin.Close();
                    Shutdown(0);
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    progWin.Close();
                    var toastErr = new ToastNotification("Extraction Failed", ex.Message);
                    toastErr.Closed += (s, ev) => Shutdown(1);
                    toastErr.Show();
                });
            }
        });
    }

    private void ExecutePdfToImages(string targetFile, string format)
    {
        string fmt = string.IsNullOrEmpty(format) ? "png" : format;
        var cts = new CancellationTokenSource();
        var progWin = new ProgressWindow($"Rendering PDF to {fmt.ToUpperInvariant()}", targetFile, cts);
        progWin.Show();

        Task.Run(() =>
        {
            try
            {
                var files = PdfProcessor.PdfToImages(targetFile, fmt, 150, (p, status) =>
                {
                    progWin.ReportProgress(p, status);
                }, cts.Token);

                Dispatcher.Invoke(() =>
                {
                    progWin.Close();
                    string first = files.Count > 0 ? files[0] : targetFile;
                    ShowToastAndExit($"Rendered {files.Count} Page(s) to {fmt.ToUpperInvariant()}", Path.GetFileName(first), first);
                });
            }
            catch (OperationCanceledException)
            {
                Dispatcher.Invoke(() =>
                {
                    progWin.Close();
                    Shutdown(0);
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    progWin.Close();
                    var toastErr = new ToastNotification("PDF Rendering Failed", ex.Message);
                    toastErr.Closed += (s, ev) => Shutdown(1);
                    toastErr.Show();
                });
            }
        });
    }

    private void ExecutePdfSplit(string targetFile)
    {
        var cts = new CancellationTokenSource();
        var progWin = new ProgressWindow("Splitting PDF Pages", targetFile, cts);
        progWin.Show();

        Task.Run(() =>
        {
            try
            {
                var files = PdfProcessor.SplitPdf(targetFile, (p, status) =>
                {
                    progWin.ReportProgress(p, status);
                }, cts.Token);

                Dispatcher.Invoke(() =>
                {
                    progWin.Close();
                    string first = files.Count > 0 ? files[0] : targetFile;
                    ShowToastAndExit($"Split into {files.Count} PDF Page(s)", Path.GetFileName(first), first);
                });
            }
            catch (OperationCanceledException)
            {
                Dispatcher.Invoke(() =>
                {
                    progWin.Close();
                    Shutdown(0);
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    progWin.Close();
                    var toastErr = new ToastNotification("PDF Split Failed", ex.Message);
                    toastErr.Closed += (s, ev) => Shutdown(1);
                    toastErr.Show();
                });
            }
        });
    }

    private void ShowToastAndExit(string title, string subtitle, string? filePath = null)
    {
        var toast = new ToastNotification(title, subtitle, filePath);
        toast.Closed += (s, ev) => Shutdown(0);
        toast.Show();
    }

    private static List<string> ParseExistingFiles(List<string> tokens)
    {
        var list = new List<string>();
        if (tokens.Count == 0) return list;

        // Try single path with all tokens joined (handles unquoted paths with spaces)
        string joined = string.Join(" ", tokens).Trim('"', '\'');
        if (File.Exists(joined))
        {
            list.Add(joined);
            return list;
        }

        // Otherwise check each individual token
        foreach (var t in tokens)
        {
            string clean = t.Trim('"', '\'');
            if (File.Exists(clean))
            {
                list.Add(clean);
            }
        }

        return list;
    }
}
