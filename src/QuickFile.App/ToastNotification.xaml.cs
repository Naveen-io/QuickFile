using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace QuickFile.App;

public partial class ToastNotification : Window
{
    private readonly string? _filePath;
    private readonly DispatcherTimer _timer;
    private bool _isClosing = false;

    public ToastNotification(string title, string subtitle, string? filePath = null)
    {
        InitializeComponent();
        _filePath = filePath;
        TxtTitle.Text = title;
        TxtSubtitle.Text = subtitle;

        if (string.IsNullOrEmpty(_filePath) || !File.Exists(_filePath))
        {
            BtnOpenFile.Visibility = Visibility.Collapsed;
            BtnOpenFolder.Visibility = Visibility.Collapsed;
        }

        // Position at bottom-right corner of work area
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 16;
        Top = workArea.Bottom - Height - 16;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(4.2)
        };
        _timer.Tick += (s, e) =>
        {
            _timer.Stop();
            AnimateClose();
        };
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // 1. Entrance animation: slide up + fade in
        double initialTop = Top + 20;
        double targetTop = Top;
        Top = initialTop;

        var slideAnim = new DoubleAnimation(initialTop, targetTop, TimeSpan.FromMilliseconds(300))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        var fadeAnim = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(250))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        BeginAnimation(TopProperty, slideAnim);
        MainContainer.BeginAnimation(OpacityProperty, fadeAnim);

        // 2. Trigger animated vector checkmark
        if (Resources["CheckmarkAnim"] is Storyboard sb)
        {
            sb.Begin(this);
        }

        _timer.Start();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Window_MouseEnter(object sender, MouseEventArgs e)
    {
        _timer.Stop();
    }

    private void Window_MouseLeave(object sender, MouseEventArgs e)
    {
        if (!_isClosing)
        {
            _timer.Interval = TimeSpan.FromSeconds(2.0);
            _timer.Start();
        }
    }

    private void BtnOpenFile_Click(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        if (!string.IsNullOrEmpty(_filePath) && File.Exists(_filePath))
        {
            try
            {
                Process.Start(new ProcessStartInfo(_filePath) { UseShellExecute = true });
            }
            catch { }
        }
        AnimateClose();
    }

    private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        if (!string.IsNullOrEmpty(_filePath) && File.Exists(_filePath))
        {
            try
            {
                Process.Start("explorer.exe", $"/select,\"{_filePath}\"");
            }
            catch { }
        }
        AnimateClose();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        AnimateClose();
    }

    private void AnimateClose()
    {
        if (_isClosing) return;
        _isClosing = true;

        var fadeOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };
        fadeOut.Completed += (s, e) => Close();
        MainContainer.BeginAnimation(OpacityProperty, fadeOut);
    }
}
