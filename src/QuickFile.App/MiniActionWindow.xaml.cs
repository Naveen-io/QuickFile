using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
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
    private readonly ImageFileInfo _info;
    private bool _updatingDimensions = false;
    private string? _lastOutputFile;

    public MiniActionWindow(string action, string filePath)
    {
        InitializeComponent();

        _action = action.ToLowerInvariant();
        _filePath = filePath;
        _info = ImageProcessor.GetImageInfo(filePath);

        TxtFileName.Text = _info.FileName;
        TxtFileSpecs.Text = $"{_info.FormattedSize} • {_info.Dimensions} • {_info.Format}";

        ConfigureView();
        PositionNearCursor();
    }

    private void PositionNearCursor()
    {
        var workArea = SystemParameters.WorkArea;
        double targetX = (workArea.Width - Width) / 2;
        double targetY = (workArea.Height - 300) / 2;

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

            if (targetY + 350 > workArea.Bottom - 10)
            {
                targetY = workArea.Bottom - 360;
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
                long halfKb = Math.Max(50, _info.SizeBytes / (2 * 1024));
                TxtTargetSizeVal.Text = Math.Min(halfKb, 500).ToString();
                break;

            case "resize":
            case "resize-custom":
                TxtActionTitle.Text = _action == "resize-custom" ? "Custom Dimensions" : "Resize Image";
                PanelResize.Visibility = Visibility.Visible;
                BtnPrimary.Content = "Resize Image";
                _updatingDimensions = true;
                TxtResizeWidth.Text = _info.Width > 0 ? _info.Width.ToString() : "1920";
                TxtResizeHeight.Text = _info.Height > 0 ? _info.Height.ToString() : "1080";
                _updatingDimensions = false;
                break;

            case "info":
                TxtActionTitle.Text = "File Information";
                PanelInfo.Visibility = Visibility.Visible;
                BtnPrimary.Content = "Copy Path";
                BtnSecondary.Content = "Close";
                InfoFormat.Text = _info.Format;
                InfoDimensions.Text = _info.Dimensions;
                InfoAspectRatio.Text = _info.AspectRatio;
                InfoSize.Text = $"{_info.FormattedSize} ({_info.SizeBytes:N0} bytes)";
                InfoModified.Text = _info.LastModified.ToString("g");
                InfoPath.Text = _info.FullPath;
                break;
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
        if (TxtCompressEstimate == null || _info == null) return;
        int q = (int)SliderQuality.Value;
        double reduction = (100 - q) * 0.75 + 15;
        long estimatedBytes = (long)(_info.SizeBytes * (1.0 - (reduction / 100.0)));
        string sizeStr = estimatedBytes switch
        {
            < 1024 => $"{estimatedBytes} B",
            < 1024 * 1024 => $"{estimatedBytes / 1024.0:F1} KB",
            _ => $"{estimatedBytes / (1024.0 * 1024.0):F2} MB"
        };
        TxtCompressEstimate.Text = $"Est: ~{sizeStr} (-{reduction:F0}%)";
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
        if (sender is Button btn && btn.Tag is string tag && double.TryParse(tag, out double scale))
        {
            if (_info.Width > 0 && _info.Height > 0)
            {
                _updatingDimensions = true;
                TxtResizeWidth.Text = Math.Max(1, (int)(_info.Width * scale)).ToString();
                TxtResizeHeight.Text = Math.Max(1, (int)(_info.Height * scale)).ToString();
                _updatingDimensions = false;
            }
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
        if (_updatingDimensions || _info == null || ChkKeepAspect == null || ChkKeepAspect.IsChecked != true || _info.Width <= 0 || _info.Height <= 0) return;
        if (int.TryParse(TxtResizeWidth.Text, out int newWidth) && newWidth > 0)
        {
            _updatingDimensions = true;
            double ratio = (double)_info.Height / _info.Width;
            TxtResizeHeight.Text = Math.Max(1, (int)Math.Round(newWidth * ratio)).ToString();
            _updatingDimensions = false;
        }
    }

    private void TxtResizeHeight_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_updatingDimensions || _info == null || ChkKeepAspect == null || ChkKeepAspect.IsChecked != true || _info.Width <= 0 || _info.Height <= 0) return;
        if (int.TryParse(TxtResizeHeight.Text, out int newHeight) && newHeight > 0)
        {
            _updatingDimensions = true;
            double ratio = (double)_info.Width / _info.Height;
            TxtResizeWidth.Text = Math.Max(1, (int)Math.Round(newHeight * ratio)).ToString();
            _updatingDimensions = false;
        }
    }

    private void BtnPrimary_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            switch (_action)
            {
                case "compress":
                    int quality = (int)SliderQuality.Value;
                    string? format = CmbCompressFormat.SelectedIndex switch
                    {
                        0 => "webp",
                        1 => "jpg",
                        2 => "png",
                        _ => null
                    };
                    string outCompressed = ImageProcessor.CompressImage(_filePath, quality, format);
                    ShowSuccess("Image Compressed", outCompressed);
                    break;

                case "compress-target":
                    if (!double.TryParse(TxtTargetSizeVal.Text, out double sizeVal) || sizeVal <= 0)
                    {
                        MessageBox.Show(this, "Please enter a valid positive number for target size.", "Invalid Size", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    long targetBytes = CmbTargetUnit.SelectedIndex == 1 
                        ? (long)(sizeVal * 1024 * 1024) 
                        : (long)(sizeVal * 1024);

                    string outTarget = ImageProcessor.CompressToTargetSize(_filePath, targetBytes);
                    ShowSuccess("Compressed to Target Size", outTarget);
                    break;

                case "resize":
                case "resize-custom":
                    if (!int.TryParse(TxtResizeWidth.Text, out int w) || w <= 0 ||
                        !int.TryParse(TxtResizeHeight.Text, out int h) || h <= 0)
                    {
                        MessageBox.Show(this, "Please enter valid positive dimensions.", "Invalid Dimensions", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    string outResized = ImageProcessor.ResizeImage(_filePath, w, h);
                    ShowSuccess("Image Resized", outResized);
                    break;

                case "info":
                    Clipboard.SetText(_filePath);
                    Close();
                    break;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to process image:\n{ex.Message}", "QuickFile Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ShowSuccess(string title, string outputFile)
    {
        _lastOutputFile = outputFile;
        var outFi = new FileInfo(outputFile);
        string sizeStr = outFi.Length switch
        {
            < 1024 => $"{outFi.Length} B",
            < 1024 * 1024 => $"{outFi.Length / 1024.0:F1} KB",
            _ => $"{outFi.Length / (1024.0 * 1024.0):F2} MB"
        };

        TxtSuccessMsg.Text = $"{title} Successfully!";
        TxtSuccessDetail.Text = $"{outFi.Name} ({sizeStr})";
        BannerSuccess.Visibility = Visibility.Visible;

        BtnPrimary.Content = "Done";
        BtnPrimary.Click -= BtnPrimary_Click;
        BtnPrimary.Click += (s, e) => Close();
        BtnSecondary.Visibility = Visibility.Collapsed;
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
}
