using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
                var toast = new ToastNotification("QuickFile Registered", "Context menu is now active on direct right-click!");
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
                var toast = new ToastNotification("QuickFile Unregistered", "Context menu removed from File Explorer.");
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

        // Action dispatch
        string action = "";
        string format = "";
        var fileTokens = new System.Collections.Generic.List<string>();

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
            else if (!args[i].StartsWith("--"))
            {
                fileTokens.Add(args[i]);
            }
        }

        string targetFile = "";
        if (fileTokens.Count > 0)
        {
            // First check if all non-flag tokens joined together form an existing file (handles unquoted paths with spaces)
            string joined = string.Join(" ", fileTokens).Trim('"', '\'');
            if (File.Exists(joined))
            {
                targetFile = joined;
            }
            else
            {
                string first = fileTokens[0].Trim('"', '\'');
                if (File.Exists(first))
                {
                    targetFile = first;
                }
            }
        }

        if (string.IsNullOrEmpty(targetFile) || !File.Exists(targetFile))
        {
            var toastErr = new ToastNotification("File Not Found", $"Could not find: '{Path.GetFileName(targetFile)}'");
            toastErr.Closed += (s, ev) => Shutdown(1);
            toastErr.Show();
            return;
        }

        try
        {
            switch (action.ToLowerInvariant())
            {
                case "copy-path":
                    Clipboard.SetText(targetFile);
                    var toastCopy = new ToastNotification("Path Copied to Clipboard", Path.GetFileName(targetFile));
                    toastCopy.Closed += (s, ev) => Shutdown(0);
                    toastCopy.Show();
                    break;

                case "convert":
                    if (string.IsNullOrEmpty(format)) format = "png";
                    string output = ImageProcessor.ConvertImage(targetFile, format);
                    var outFi = new FileInfo(output);
                    string sizeStr = outFi.Length switch
                    {
                        < 1024 => $"{outFi.Length} B",
                        < 1024 * 1024 => $"{outFi.Length / 1024.0:F1} KB",
                        _ => $"{outFi.Length / (1024.0 * 1024.0):F2} MB"
                    };
                    var toastConv = new ToastNotification($"Converted to {format.ToUpperInvariant()} ({sizeStr})", outFi.Name, output);
                    toastConv.Closed += (s, ev) => Shutdown(0);
                    toastConv.Show();
                    break;

                case "compress":
                case "compress-target":
                case "resize":
                case "resize-custom":
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
        catch (Exception ex)
        {
            var toastEx = new ToastNotification("QuickFile Error", ex.Message);
            toastEx.Closed += (s, ev) => Shutdown(1);
            toastEx.Show();
        }
    }
}
