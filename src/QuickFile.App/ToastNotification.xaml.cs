using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace QuickFile.App;

public partial class ToastNotification : Window
{
    private readonly string? _filePathToSelect;
    private readonly DispatcherTimer _timer;

    public ToastNotification(string title, string subtitle, string? filePathToSelect = null)
    {
        InitializeComponent();
        _filePathToSelect = filePathToSelect;
        TxtTitle.Text = title;
        TxtSubtitle.Text = subtitle;

        if (string.IsNullOrEmpty(_filePathToSelect) || !File.Exists(_filePathToSelect))
        {
            BtnOpenFolder.Visibility = Visibility.Collapsed;
        }

        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 20;
        Top = workArea.Bottom - Height - 20;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3.2)
        };
        _timer.Tick += (s, e) =>
        {
            _timer.Stop();
            Close();
        };
        _timer.Start();
    }

    private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        if (!string.IsNullOrEmpty(_filePathToSelect) && File.Exists(_filePathToSelect))
        {
            try
            {
                Process.Start("explorer.exe", $"/select,\"{_filePathToSelect}\"");
            }
            catch { }
        }
        Close();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        Close();
    }
}
