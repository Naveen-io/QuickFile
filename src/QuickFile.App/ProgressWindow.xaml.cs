using System;
using System.IO;
using System.Threading;
using System.Windows;

namespace QuickFile.App;

public partial class ProgressWindow : Window
{
    private readonly CancellationTokenSource _cts;

    public ProgressWindow(string title, string filePath, CancellationTokenSource cts)
    {
        InitializeComponent();
        _cts = cts;
        TxtTitle.Text = title;
        TxtFileName.Text = Path.GetFileName(filePath);
    }

    public void ReportProgress(double fraction, string status)
    {
        Dispatcher.Invoke(() =>
        {
            int p = Math.Clamp((int)(fraction * 100), 0, 100);
            ProgressBarCtrl.Value = p;
            TxtPercent.Text = $"{p}%";
            TxtStatus.Text = status;
        });
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        _cts.Cancel();
        TxtStatus.Text = "Cancelling...";
        IsEnabled = false;
    }
}
