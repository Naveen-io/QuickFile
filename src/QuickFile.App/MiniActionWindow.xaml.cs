using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using QuickFile.Core;

namespace QuickFile.App;

public partial class MiniActionWindow : Window
{
    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    private readonly string _action;
    private readonly string _filePath;
    private readonly string _ext;
    private bool _updatingDimensions = false;
    private string? _lastOutputFile;

    // Info objects
    private ImageFileInfo? _imgInfo;
    private MediaProbeInfo? _mediaInfo;
    private PdfFileInfo? _pdfInfo;

    public MiniActionWindow(string action, string filePath)
    {
        InitializeComponent();

        _action = action.ToLowerInvariant();
        _filePath = filePath;
        _ext = Path.GetExtension(filePath).ToLowerInvariant();

        LoadFileInfo();
        ConfigureView();
        PositionNearCursor();
    }

    private void LoadFileInfo()
    {
        TxtFileName.Text = Path.GetFileName(_filePath);

        if (IsImageExt(_ext))
        {
            _imgInfo = ImageProcessor.GetImageInfo(_filePath);
            TxtFileSpecs.Text = $"{_imgInfo.FormattedSize} • {_imgInfo.Dimensions} • {_imgInfo.Format}";
        }
        else if (IsAudioExt(_ext) || IsVideoExt(_ext))
        {
            try
            {
                _mediaInfo = FFmpegHelper.ProbeFile(_filePath);
                string specs = IsVideoExt(_ext)
                    ? $"{_mediaInfo.FormattedSize} • {_mediaInfo.Resolution} • {_mediaInfo.FormattedDuration}"
                    : $"{_mediaInfo.FormattedSize} • {_mediaInfo.FormattedDuration} • {_mediaInfo.ChannelDescription}";
                TxtFileSpecs.Text = specs;
            }
            catch
            {
                var fi = new FileInfo(_filePath);
                TxtFileSpecs.Text = $"{FilePathHelper.FormatFileSize(fi.Length)} • {_ext.TrimStart('.').ToUpperInvariant()}";
            }
        }
        else if (_ext == ".pdf")
        {
            try
            {
                _pdfInfo = PdfProcessor.GetPdfInfo(_filePath);
                TxtFileSpecs.Text = $"{_pdfInfo.FormattedSize} • {_pdfInfo.PageCount} Page(s) • PDF";
            }
            catch
            {
                var fi = new FileInfo(_filePath);
                TxtFileSpecs.Text = $"{FilePathHelper.FormatFileSize(fi.Length)} • PDF";
            }
        }
        else
        {
            var fi = new FileInfo(_filePath);
            TxtFileSpecs.Text = $"{FilePathHelper.FormatFileSize(fi.Length)} • {_ext.TrimStart('.').ToUpperInvariant()}";
        }
    }

    private void PositionNearCursor()
    {
        var workArea = SystemParameters.WorkArea;
        double targetX = (workArea.Width - Width) / 2;
        double targetY = (workArea.Height - 340) / 2;

        if (GetCursorPos(out POINT pt))
        {
            targetX = pt.X + 20;
            targetY = pt.Y - 20;

            if (targetX + Width > workArea.Right - 10)
            {
                targetX = pt.X - Width - 20;
            }
            if (targetX < workArea.Left + 10)
            {
                targetX = workArea.Left + 10;
            }

            if (targetY + 400 > workArea.Bottom - 10)
            {
                targetY = workArea.Bottom - 410;
            }
            if (targetY < workArea.Top + 10)
            {
                targetY = workArea.Top + 10;
            }
        }

        Left = targetX;
        Top = targetY;
    }

    private void ConfigureView()
    {
        switch (_action)
        {
            case "compress":
                TxtActionTitle.Text = "Compress Image";
                PanelCompress.Visibility = Visibility.Visible;
                BtnPrimary.Content = "Compress Image";
                UpdateCompressEstimate();
                break;

            case "compress-target":
                TxtActionTitle.Text = "Compress to Target Size";
                PanelCompressTarget.Visibility = Visibility.Visible;
                BtnPrimary.Content = "Compress to Size";
                if (_imgInfo != null)
                {
                    long halfKb = Math.Max(50, _imgInfo.SizeBytes / (2 * 1024));
                    TxtTargetSizeVal.Text = Math.Min(halfKb, 500).ToString();
                }
                break;

            case "resize":
            case "resize-custom":
                TxtActionTitle.Text = _action == "resize-custom" ? "Custom Dimensions" : "Resize Image";
                PanelResize.Visibility = Visibility.Visible;
                BtnPrimary.Content = "Resize Image";
                _updatingDimensions = true;
                if (_imgInfo != null)
                {
                    TxtResizeWidth.Text = _imgInfo.Width > 0 ? _imgInfo.Width.ToString() : "1920";
                    TxtResizeHeight.Text = _imgInfo.Height > 0 ? _imgInfo.Height.ToString() : "1080";
                }
                _updatingDimensions = false;
                break;

            case "audio-options":
                TxtActionTitle.Text = "Audio Options & Presets";
                PanelAudio.Visibility = Visibility.Visible;
                BtnPrimary.Content = "Convert Audio";
                break;

            case "video-options":
                TxtActionTitle.Text = "Video Options & Presets";
                PanelVideo.Visibility = Visibility.Visible;
                BtnPrimary.Content = "Convert Video";
                break;

            case "pdf-tools":
                TxtActionTitle.Text = "PDF Tools & Processing";
                PanelPdf.Visibility = Visibility.Visible;
                BtnPrimary.Content = "Process PDF";
                break;

            case "info":
                TxtActionTitle.Text = "File Information";
                PanelInfo.Visibility = Visibility.Visible;
                BtnPrimary.Content = "Copy Path";
                BtnSecondary.Content = "Close";
                PopulateInfoPanel();
                break;

            default:
                // Auto-route based on file type
                if (IsAudioExt(_ext))
                {
                    TxtActionTitle.Text = "Audio Options";
                    PanelAudio.Visibility = Visibility.Visible;
                    BtnPrimary.Content = "Convert Audio";
                }
                else if (IsVideoExt(_ext))
                {
                    TxtActionTitle.Text = "Video Options";
                    PanelVideo.Visibility = Visibility.Visible;
                    BtnPrimary.Content = "Convert Video";
                }
                else if (_ext == ".pdf")
                {
                    TxtActionTitle.Text = "PDF Tools";
                    PanelPdf.Visibility = Visibility.Visible;
                    BtnPrimary.Content = "Process PDF";
                }
                else
                {
                    TxtActionTitle.Text = "QuickFile Action";
                    PanelCompress.Visibility = Visibility.Visible;
                    BtnPrimary.Content = "Compress Image";
                }
                break;
        }
    }

    private void PopulateInfoPanel()
    {
        var fi = new FileInfo(_filePath);
        InfoSize.Text = $"{FilePathHelper.FormatFileSize(fi.Length)} ({fi.Length:N0} bytes)";
        InfoModified.Text = fi.LastWriteTime.ToString("g");
        InfoPath.Text = fi.FullName;

        if (_imgInfo != null)
        {
            InfoFormat.Text = _imgInfo.Format;
            InfoDimensions.Text = _imgInfo.Dimensions;
            InfoExtra.Text = $"Aspect: {_imgInfo.AspectRatio} • Alpha: {(_imgInfo.HasAlpha ? "Yes" : "No")}";
        }
        else if (_mediaInfo != null)
        {
            InfoFormat.Text = _mediaInfo.ContainerFormat;
            InfoDimensions.Text = IsVideoExt(_ext) ? _mediaInfo.Resolution : _mediaInfo.ChannelDescription;
            InfoExtra.Text = $"Duration: {_mediaInfo.FormattedDuration} • Bitrate: {_mediaInfo.FormattedBitrate}";
        }
        else if (_pdfInfo != null)
        {
            InfoFormat.Text = "PDF Document";
            InfoDimensions.Text = $"{_pdfInfo.PageCount} Page(s)";
            InfoExtra.Text = string.IsNullOrEmpty(_pdfInfo.Title) ? "Standard Document" : _pdfInfo.Title;
        }
        else
        {
            InfoFormat.Text = _ext.TrimStart('.').ToUpperInvariant();
            InfoDimensions.Text = "Standard File";
            InfoExtra.Text = "N/A";
        }
    }

    private void SliderQuality_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtQualityVal != null)
        {
            int q = (int)SliderQuality.Value;
            TxtQualityVal.Text = $"{q}%";
            UpdateCompressEstimate();
        }
    }

    private void UpdateCompressEstimate()
    {
        if (TxtCompressEstimate == null || _imgInfo == null) return;
        int q = (int)SliderQuality.Value;
        double reduction = (100 - q) * 0.75 + 15;
        long estimatedBytes = (long)(_imgInfo.SizeBytes * (1.0 - (reduction / 100.0)));
        TxtCompressEstimate.Text = $"Est: ~{FilePathHelper.FormatFileSize(estimatedBytes)} (-{reduction:F0}%)";
    }

    private void PresetSize_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
        {
            TxtTargetSizeVal.Text = tag;
            CmbTargetUnit.SelectedIndex = 0; // KB
        }
    }

    private void PresetScale_Click(object sender, RoutedEventArgs e)
    {
        if (_imgInfo == null || _imgInfo.Width <= 0 || _imgInfo.Height <= 0) return;
        if (sender is Button btn && btn.Tag is string tag && double.TryParse(tag, out double factor))
        {
            _updatingDimensions = true;
            TxtResizeWidth.Text = Math.Max(16, (int)(_imgInfo.Width * factor)).ToString();
            TxtResizeHeight.Text = Math.Max(16, (int)(_imgInfo.Height * factor)).ToString();
            _updatingDimensions = false;
        }
    }

    private void PresetDim_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
        {
            var parts = tag.Split('x');
            if (parts.Length == 2)
            {
                _updatingDimensions = true;
                TxtResizeWidth.Text = parts[0];
                TxtResizeHeight.Text = parts[1];
                _updatingDimensions = false;
            }
        }
    }

    private void TxtResizeWidth_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_updatingDimensions || _imgInfo == null || ChkKeepAspect == null || ChkKeepAspect.IsChecked != true) return;
        if (_imgInfo.Width <= 0 || _imgInfo.Height <= 0) return;

        if (int.TryParse(TxtResizeWidth.Text, out int w) && w > 0)
        {
            _updatingDimensions = true;
            int h = (int)Math.Round((double)w * _imgInfo.Height / _imgInfo.Width);
            TxtResizeHeight.Text = h.ToString();
            _updatingDimensions = false;
        }
    }

    private void TxtResizeHeight_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_updatingDimensions || _imgInfo == null || ChkKeepAspect == null || ChkKeepAspect.IsChecked != true) return;
        if (_imgInfo.Width <= 0 || _imgInfo.Height <= 0) return;

        if (int.TryParse(TxtResizeHeight.Text, out int h) && h > 0)
        {
            _updatingDimensions = true;
            int w = (int)Math.Round((double)h * _imgInfo.Width / _imgInfo.Height);
            TxtResizeWidth.Text = w.ToString();
            _updatingDimensions = false;
        }
    }

    private void CmbPdfAction_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SubPanelPdfImages == null) return;
        int idx = CmbPdfAction.SelectedIndex;
        SubPanelPdfImages.Visibility = idx == 0 ? Visibility.Visible : Visibility.Collapsed;
        SubPanelPdfExtract.Visibility = idx == 2 ? Visibility.Visible : Visibility.Collapsed;
        SubPanelPdfRotate.Visibility = idx == 3 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BtnPrimary_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_action == "info")
            {
                Clipboard.SetText(_filePath);
                ShowSuccess("File path copied to clipboard!", _filePath);
                return;
            }

            BtnPrimary.IsEnabled = false;

            if (PanelCompress.Visibility == Visibility.Visible)
            {
                int q = (int)SliderQuality.Value;
                string fmt = CmbCompressFormat.SelectedIndex switch
                {
                    0 => "webp",
                    1 => "jpg",
                    _ => "png"
                };
                string outPath = ImageProcessor.CompressImage(_filePath, q, fmt);
                ShowSuccess("Image compressed successfully!", outPath);
            }
            else if (PanelCompressTarget.Visibility == Visibility.Visible)
            {
                if (!long.TryParse(TxtTargetSizeVal.Text, out long unitVal) || unitVal <= 0)
                {
                    MessageBox.Show("Please enter a valid positive number for target size.", "QuickFile", MessageBoxButton.OK, MessageBoxImage.Warning);
                    BtnPrimary.IsEnabled = true;
                    return;
                }
                long bytes = CmbTargetUnit.SelectedIndex == 1 ? unitVal * 1024 * 1024 : unitVal * 1024;
                string outPath = ImageProcessor.CompressToTargetSize(_filePath, bytes);
                ShowSuccess("Compressed to target size successfully!", outPath);
            }
            else if (PanelResize.Visibility == Visibility.Visible)
            {
                if (!int.TryParse(TxtResizeWidth.Text, out int w) || !int.TryParse(TxtResizeHeight.Text, out int h) || w <= 0 || h <= 0)
                {
                    MessageBox.Show("Please enter valid positive dimensions.", "QuickFile", MessageBoxButton.OK, MessageBoxImage.Warning);
                    BtnPrimary.IsEnabled = true;
                    return;
                }
                string outPath = ImageProcessor.ResizeImage(_filePath, w, h);
                ShowSuccess("Image resized successfully!", outPath);
            }
            else if (PanelAudio.Visibility == Visibility.Visible)
            {
                string targetFmt = CmbAudioFormat.SelectedIndex switch
                {
                    0 => "mp3",
                    1 => "wav",
                    2 => "flac",
                    3 => "aac",
                    4 => "m4a",
                    5 => "ogg",
                    _ => "mp3"
                };

                var preset = CmbAudioPreset.SelectedIndex switch
                {
                    0 => AudioPreset.Smaller,
                    2 => AudioPreset.HighQuality,
                    _ => AudioPreset.Balanced
                };

                int? sr = CmbAudioSampleRate.SelectedIndex switch
                {
                    1 => 44100,
                    2 => 48000,
                    3 => 96000,
                    _ => null
                };

                int? ch = CmbAudioChannels.SelectedIndex switch
                {
                    1 => 2,
                    2 => 1,
                    _ => null
                };

                var opts = new AudioConversionOptions
                {
                    TargetFormat = targetFmt,
                    Preset = preset,
                    SampleRateHz = sr,
                    Channels = ch
                };

                string outPath = AudioProcessor.ConvertAudioWithOptions(_filePath, opts);
                ShowSuccess($"Audio converted to {targetFmt.ToUpperInvariant()} successfully!", outPath);
            }
            else if (PanelVideo.Visibility == Visibility.Visible)
            {
                if (ChkExtractAudioOnly.IsChecked == true)
                {
                    string outPath = VideoProcessor.ExtractAudio(_filePath, "mp3");
                    ShowSuccess("Audio extracted to MP3 successfully!", outPath);
                }
                else
                {
                    string targetFmt = CmbVideoFormat.SelectedIndex switch
                    {
                        0 => "mp4",
                        1 => "mkv",
                        2 => "webm",
                        3 => "mov",
                        4 => "avi",
                        _ => "mp4"
                    };

                    var preset = CmbVideoPreset.SelectedIndex switch
                    {
                        0 => VideoPreset.Smaller,
                        2 => VideoPreset.HighQuality,
                        _ => VideoPreset.Balanced
                    };

                    var res = CmbVideoResolution.SelectedIndex switch
                    {
                        1 => VideoResolution.P1080,
                        2 => VideoResolution.P720,
                        3 => VideoResolution.P480,
                        _ => VideoResolution.Original
                    };

                    var opts = new VideoConversionOptions
                    {
                        TargetFormat = targetFmt,
                        Preset = preset,
                        Resolution = res
                    };

                    string outPath = VideoProcessor.ConvertVideoWithOptions(_filePath, opts);
                    ShowSuccess($"Video converted to {targetFmt.ToUpperInvariant()} successfully!", outPath);
                }
            }
            else if (PanelPdf.Visibility == Visibility.Visible)
            {
                int actIdx = CmbPdfAction.SelectedIndex;
                if (actIdx == 0) // PDF to Images
                {
                    string fmt = CmbPdfImgFormat.SelectedIndex switch
                    {
                        1 => "jpg",
                        2 => "webp",
                        _ => "png"
                    };
                    int dpi = CmbPdfDpi.SelectedIndex switch
                    {
                        1 => 300,
                        2 => 72,
                        _ => 150
                    };
                    var files = PdfProcessor.PdfToImages(_filePath, fmt, dpi);
                    string first = files.Count > 0 ? files[0] : _filePath;
                    ShowSuccess($"Converted {files.Count} page(s) to {fmt.ToUpperInvariant()} images!", first);
                }
                else if (actIdx == 1) // Split PDF
                {
                    var files = PdfProcessor.SplitPdf(_filePath);
                    string first = files.Count > 0 ? files[0] : _filePath;
                    ShowSuccess($"Split into {files.Count} individual page PDF(s)!", first);
                }
                else if (actIdx == 2) // Extract Pages
                {
                    string range = TxtPdfPageRange.Text.Trim();
                    string outPath = PdfProcessor.ExtractPages(_filePath, range);
                    ShowSuccess($"Extracted pages ({range}) to new PDF!", outPath);
                }
                else if (actIdx == 3) // Rotate
                {
                    int angle = CmbPdfRotateAngle.SelectedIndex switch
                    {
                        1 => 180,
                        2 => 270,
                        _ => 90
                    };
                    string outPath = PdfProcessor.RotatePages(_filePath, angle);
                    ShowSuccess($"Rotated pages by {angle}° successfully!", outPath);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Operation error: {ex.Message}", "QuickFile Error", MessageBoxButton.OK, MessageBoxImage.Error);
            BtnPrimary.IsEnabled = true;
        }
    }

    private void ShowSuccess(string message, string outputPath)
    {
        _lastOutputFile = outputPath;
        TxtSuccessMsg.Text = message;
        TxtSuccessDetail.Text = Path.GetFileName(outputPath);
        BannerSuccess.Visibility = Visibility.Visible;
        BtnPrimary.Visibility = Visibility.Collapsed;
        BtnSecondary.Content = "Close";
    }

    private void BtnSuccessOpen_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_lastOutputFile) && File.Exists(_lastOutputFile))
        {
            try
            {
                Process.Start("explorer.exe", $"/select,\"{_lastOutputFile}\"");
            }
            catch { }
        }
        Close();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private static bool IsImageExt(string ext) => Array.IndexOf(ShellManager.ImageExtensions, ext) >= 0;
    private static bool IsAudioExt(string ext) => Array.IndexOf(ShellManager.AudioExtensions, ext) >= 0;
    private static bool IsVideoExt(string ext) => Array.IndexOf(ShellManager.VideoExtensions, ext) >= 0;
}
