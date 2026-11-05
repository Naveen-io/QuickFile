using System;
using System.IO;

namespace QuickFile.Core;

public static class FilePathHelper
{
    public static string GenerateUniquePath(string dir, string prefix, string ext)
    {
        if (!ext.StartsWith('.')) ext = "." + ext;
        int counter = 1;
        string path;
        do
        {
            path = Path.Combine(dir, $"{prefix} ({counter}){ext}");
            counter++;
        } while (File.Exists(path));
        return path;
    }

    public static string FormatFileSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        _ => $"{bytes / (1024.0 * 1024.0):F2} MB"
    };
}
