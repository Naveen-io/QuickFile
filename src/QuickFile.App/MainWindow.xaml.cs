using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using QuickFile.Core;

namespace QuickFile.App;

public partial class MainWindow : Window
{
    private readonly string _exePath;

    public MainWindow()
    {
        InitializeComponent();
        _exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "";
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        bool isRegistered = ShellManager.IsRegistered(_exePath);
        if (isRegistered)
        {
            TxtStatus.Text = "Status: Active (Integrated into Explorer)";
            TxtStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 124, 65));
            BtnToggle.Content = "Unregister";
            BtnToggle.Style = (Style)FindResource(typeof(System.Windows.Controls.Button));
        }
        else
        {
            TxtStatus.Text = "Status: Inactive (Not registered in Explorer)";
            TxtStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(180, 0, 0));
            BtnToggle.Content = "Register";
            BtnToggle.Style = (Style)FindResource("PrimaryButton");
        }
    }

    private void BtnToggle_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            bool isRegistered = ShellManager.IsRegistered(_exePath);
            if (isRegistered)
            {
                ShellManager.Unregister();
                MessageBox.Show(this, "QuickFile context menu has been completely removed from Windows Explorer.", "QuickFile", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                ShellManager.Register(_exePath);
                MessageBox.Show(this, "QuickFile cascading context menu has been successfully registered!\n\nRight-click any image file in File Explorer to see QuickFile.", "QuickFile", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            UpdateStatus();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to update context menu registration:\n{ex.Message}", "QuickFile Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnTestImage_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string testDir = Path.Combine(Path.GetTempPath(), "QuickFileSamples");
            Directory.CreateDirectory(testDir);
            string sampleFile = Path.Combine(testDir, "sample_image.png");

            if (!File.Exists(sampleFile))
            {
                byte[] pngBytes = new byte[] {
                    0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 
                    0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 
                    0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4, 0x89, 0x00, 0x00, 0x00, 
                    0x0D, 0x49, 0x48, 0x44, 0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00, 
                    0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00, 0x00, 0x00, 0x00, 0x49, 
                    0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
                };
                File.WriteAllBytes(sampleFile, pngBytes);
            }

            Process.Start("explorer.exe", $"/select,\"{sampleFile}\"");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Error creating sample: {ex.Message}", "QuickFile Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
