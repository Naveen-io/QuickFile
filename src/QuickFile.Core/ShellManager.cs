using System;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace QuickFile.Core;

[SupportedOSPlatform("windows")]
public static class ShellManager
{
    private const string ImageBaseKey = @"Software\Classes\SystemFileAssociations\image\shell\QuickFile";
    private const string AudioBaseKey = @"Software\Classes\SystemFileAssociations\audio\shell\QuickFile";
    private const string VideoBaseKey = @"Software\Classes\SystemFileAssociations\video\shell\QuickFile";
    private const string PdfBaseKey = @"Software\Classes\SystemFileAssociations\.pdf\shell\QuickFile";
    private const string Win11ClassicClsidKey = @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32";

    public static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".tiff", ".tif", ".gif" };
    public static readonly string[] AudioExtensions = { ".mp3", ".wav", ".flac", ".aac", ".m4a", ".ogg", ".wma" };
    public static readonly string[] VideoExtensions = { ".mp4", ".mkv", ".mov", ".webm", ".avi", ".wmv" };
    public static readonly string[] PdfExtensions = { ".pdf" };

    public static bool IsRegistered(string? exePath = null)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(ImageBaseKey);
            if (key == null) return false;

            if (string.IsNullOrEmpty(exePath)) return true;

            var iconVal = key.GetValue("Icon") as string;
            return iconVal != null && iconVal.Contains(exePath, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static void Register(string exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            throw new FileNotFoundException($"QuickFile executable not found at: {exePath}");
        }

        // 1. Register Image Menu
        RegisterImageMenu(ImageBaseKey, exePath);
        foreach (var ext in ImageExtensions)
        {
            RegisterImageMenu($@"Software\Classes\SystemFileAssociations\{ext}\shell\QuickFile", exePath);
        }

        // 2. Register Audio Menu
        RegisterAudioMenu(AudioBaseKey, exePath);
        foreach (var ext in AudioExtensions)
        {
            RegisterAudioMenu($@"Software\Classes\SystemFileAssociations\{ext}\shell\QuickFile", exePath);
        }

        // 3. Register Video Menu
        RegisterVideoMenu(VideoBaseKey, exePath);
        foreach (var ext in VideoExtensions)
        {
            RegisterVideoMenu($@"Software\Classes\SystemFileAssociations\{ext}\shell\QuickFile", exePath);
        }

        // 4. Register PDF Menu
        RegisterPdfMenu(PdfBaseKey, exePath);
        foreach (var ext in PdfExtensions)
        {
            RegisterPdfMenu($@"Software\Classes\SystemFileAssociations\{ext}\shell\QuickFile", exePath);
        }

        // 5. Windows 11 Direct Right-Click bypass
        EnableDirectWin11Menu();
    }

    private static void RegisterImageMenu(string regPath, string exePath)
    {
        CleanKey(regPath);
        using var key = Registry.CurrentUser.CreateSubKey(regPath, true);
        key.SetValue("MUIVerb", "QuickFile");
        key.SetValue("SubCommands", "");
        key.SetValue("Icon", $"\"{exePath}\",0");

        using var rootShell = key.CreateSubKey("shell", true);

        // Submenu 1: CONVERT
        using (var convKey = rootShell.CreateSubKey("01_Convert", true))
        {
            convKey.SetValue("MUIVerb", "Convert");
            convKey.SetValue("SubCommands", "");
            using var convShell = convKey.CreateSubKey("shell", true);
            AddMenuItem(convShell, "01_PNG", "Convert to PNG", $"\"{exePath}\" --action convert --format png \"%1\"");
            AddMenuItem(convShell, "02_JPG", "Convert to JPG", $"\"{exePath}\" --action convert --format jpg \"%1\"");
            AddMenuItem(convShell, "03_WEBP", "Convert to WEBP", $"\"{exePath}\" --action convert --format webp \"%1\"");
            AddMenuItem(convShell, "04_BMP", "Convert to BMP", $"\"{exePath}\" --action convert --format bmp \"%1\"");
            AddMenuItem(convShell, "05_PDF", "Convert to PDF", $"\"{exePath}\" --action img-to-pdf \"%1\"");
        }

        // Submenu 2: COMPRESS
        using (var compKey = rootShell.CreateSubKey("02_Compress", true))
        {
            compKey.SetValue("MUIVerb", "Compress");
            compKey.SetValue("SubCommands", "");
            using var compShell = compKey.CreateSubKey("shell", true);
            AddMenuItem(compShell, "01_Quick", "Compress image", $"\"{exePath}\" --action compress \"%1\"");
            AddMenuItem(compShell, "02_Target", "Compress to target size...", $"\"{exePath}\" --action compress-target \"%1\"");
        }

        // Submenu 3: RESIZE
        using (var rszKey = rootShell.CreateSubKey("03_Resize", true))
        {
            rszKey.SetValue("MUIVerb", "Resize");
            rszKey.SetValue("SubCommands", "");
            using var rszShell = rszKey.CreateSubKey("shell", true);
            AddMenuItem(rszShell, "01_Quick", "Resize image...", $"\"{exePath}\" --action resize \"%1\"");
            AddMenuItem(rszShell, "02_Custom", "Custom dimensions...", $"\"{exePath}\" --action resize-custom \"%1\"");
        }

        // Submenu 4: MORE ACTIONS
        using (var moreKey = rootShell.CreateSubKey("04_More", true))
        {
            moreKey.SetValue("MUIVerb", "More actions");
            moreKey.SetValue("SubCommands", "");
            using var moreShell = moreKey.CreateSubKey("shell", true);
            AddMenuItem(moreShell, "01_CopyPath", "Copy file path", $"\"{exePath}\" --action copy-path \"%1\"");
            AddMenuItem(moreShell, "02_FileInfo", "View file information", $"\"{exePath}\" --action info \"%1\"");
        }
    }

    private static void RegisterAudioMenu(string regPath, string exePath)
    {
        CleanKey(regPath);
        using var key = Registry.CurrentUser.CreateSubKey(regPath, true);
        key.SetValue("MUIVerb", "QuickFile");
        key.SetValue("SubCommands", "");
        key.SetValue("Icon", $"\"{exePath}\",0");

        using var rootShell = key.CreateSubKey("shell", true);

        // Submenu 1: CONVERT
        using (var convKey = rootShell.CreateSubKey("01_Convert", true))
        {
            convKey.SetValue("MUIVerb", "Convert");
            convKey.SetValue("SubCommands", "");
            using var convShell = convKey.CreateSubKey("shell", true);
            AddMenuItem(convShell, "01_MP3", "Convert to MP3 (Universal)", $"\"{exePath}\" --action audio-convert --format mp3 \"%1\"");
            AddMenuItem(convShell, "02_WAV", "Convert to WAV (Uncompressed)", $"\"{exePath}\" --action audio-convert --format wav \"%1\"");
            AddMenuItem(convShell, "03_FLAC", "Convert to FLAC (Lossless)", $"\"{exePath}\" --action audio-convert --format flac \"%1\"");
            AddMenuItem(convShell, "04_AAC", "Convert to AAC (High Quality)", $"\"{exePath}\" --action audio-convert --format aac \"%1\"");
            AddMenuItem(convShell, "05_M4A", "Convert to M4A (Apple)", $"\"{exePath}\" --action audio-convert --format m4a \"%1\"");
            AddMenuItem(convShell, "06_OGG", "Convert to OGG Vorbis", $"\"{exePath}\" --action audio-convert --format ogg \"%1\"");
        }

        // Submenu 2: PRESETS & OPTIONS
        using (var optKey = rootShell.CreateSubKey("02_Options", true))
        {
            optKey.SetValue("MUIVerb", "Presets & Options");
            optKey.SetValue("SubCommands", "");
            using var optShell = optKey.CreateSubKey("shell", true);
            AddMenuItem(optShell, "01_Small", "Compress audio (Smaller file - 128 kbps)", $"\"{exePath}\" --action audio-convert --format mp3 --preset smaller \"%1\"");
            AddMenuItem(optShell, "02_High", "High quality (320 kbps MP3)", $"\"{exePath}\" --action audio-convert --format mp3 --preset highquality \"%1\"");
            AddMenuItem(optShell, "03_Settings", "Audio options & custom bitrate...", $"\"{exePath}\" --action audio-options \"%1\"");
        }

        // Submenu 3: MORE ACTIONS
        using (var moreKey = rootShell.CreateSubKey("03_More", true))
        {
            moreKey.SetValue("MUIVerb", "More actions");
            moreKey.SetValue("SubCommands", "");
            using var moreShell = moreKey.CreateSubKey("shell", true);
            AddMenuItem(moreShell, "01_CopyPath", "Copy file path", $"\"{exePath}\" --action copy-path \"%1\"");
            AddMenuItem(moreShell, "02_FileInfo", "View audio information", $"\"{exePath}\" --action info \"%1\"");
        }
    }

    private static void RegisterVideoMenu(string regPath, string exePath)
    {
        CleanKey(regPath);
        using var key = Registry.CurrentUser.CreateSubKey(regPath, true);
        key.SetValue("MUIVerb", "QuickFile");
        key.SetValue("SubCommands", "");
        key.SetValue("Icon", $"\"{exePath}\",0");

        using var rootShell = key.CreateSubKey("shell", true);

        // Submenu 1: CONVERT
        using (var convKey = rootShell.CreateSubKey("01_Convert", true))
        {
            convKey.SetValue("MUIVerb", "Convert");
            convKey.SetValue("SubCommands", "");
            using var convShell = convKey.CreateSubKey("shell", true);
            AddMenuItem(convShell, "01_MP4", "Convert to MP4 (H.264/AAC)", $"\"{exePath}\" --action video-convert --format mp4 \"%1\"");
            AddMenuItem(convShell, "02_MKV", "Convert to MKV (Matroska)", $"\"{exePath}\" --action video-convert --format mkv \"%1\"");
            AddMenuItem(convShell, "03_WEBM", "Convert to WEBM (Web Video)", $"\"{exePath}\" --action video-convert --format webm \"%1\"");
            AddMenuItem(convShell, "04_MOV", "Convert to MOV (QuickTime)", $"\"{exePath}\" --action video-convert --format mov \"%1\"");
            AddMenuItem(convShell, "05_AVI", "Convert to AVI", $"\"{exePath}\" --action video-convert --format avi \"%1\"");
        }

        // Submenu 2: EXTRACT AUDIO
        using (var extKey = rootShell.CreateSubKey("02_Extract", true))
        {
            extKey.SetValue("MUIVerb", "Extract Audio");
            extKey.SetValue("SubCommands", "");
            using var extShell = extKey.CreateSubKey("shell", true);
            AddMenuItem(extShell, "01_MP3", "Extract Audio as MP3", $"\"{exePath}\" --action extract-audio --format mp3 \"%1\"");
            AddMenuItem(extShell, "02_WAV", "Extract Audio as WAV", $"\"{exePath}\" --action extract-audio --format wav \"%1\"");
            AddMenuItem(extShell, "03_AAC", "Extract Audio as AAC", $"\"{exePath}\" --action extract-audio --format aac \"%1\"");
        }

        // Submenu 3: PRESETS & RESOLUTION
        using (var optKey = rootShell.CreateSubKey("03_Presets", true))
        {
            optKey.SetValue("MUIVerb", "Presets & Resolution");
            optKey.SetValue("SubCommands", "");
            using var optShell = optKey.CreateSubKey("shell", true);
            AddMenuItem(optShell, "01_Compress", "Compress video (Smaller file)", $"\"{exePath}\" --action video-convert --format mp4 --preset smaller \"%1\"");
            AddMenuItem(optShell, "02_1080p", "Convert to 1080p Full HD", $"\"{exePath}\" --action video-convert --format mp4 --res 1080 \"%1\"");
            AddMenuItem(optShell, "03_720p", "Convert to 720p HD", $"\"{exePath}\" --action video-convert --format mp4 --res 720 \"%1\"");
            AddMenuItem(optShell, "04_Settings", "Video options & presets...", $"\"{exePath}\" --action video-options \"%1\"");
        }

        // Submenu 4: MORE ACTIONS
        using (var moreKey = rootShell.CreateSubKey("04_More", true))
        {
            moreKey.SetValue("MUIVerb", "More actions");
            moreKey.SetValue("SubCommands", "");
            using var moreShell = moreKey.CreateSubKey("shell", true);
            AddMenuItem(moreShell, "01_CopyPath", "Copy file path", $"\"{exePath}\" --action copy-path \"%1\"");
            AddMenuItem(moreShell, "02_FileInfo", "View video information", $"\"{exePath}\" --action info \"%1\"");
        }
    }

    private static void RegisterPdfMenu(string regPath, string exePath)
    {
        CleanKey(regPath);
        using var key = Registry.CurrentUser.CreateSubKey(regPath, true);
        key.SetValue("MUIVerb", "QuickFile");
        key.SetValue("SubCommands", "");
        key.SetValue("Icon", $"\"{exePath}\",0");

        using var rootShell = key.CreateSubKey("shell", true);

        // Submenu 1: CONVERT
        using (var convKey = rootShell.CreateSubKey("01_Convert", true))
        {
            convKey.SetValue("MUIVerb", "Convert");
            convKey.SetValue("SubCommands", "");
            using var convShell = convKey.CreateSubKey("shell", true);
            AddMenuItem(convShell, "01_PNG", "Convert to PNG Images", $"\"{exePath}\" --action pdf-to-images --format png \"%1\"");
            AddMenuItem(convShell, "02_JPG", "Convert to JPG Images", $"\"{exePath}\" --action pdf-to-images --format jpg \"%1\"");
        }

        // Submenu 2: PAGE OPERATIONS
        using (var pageKey = rootShell.CreateSubKey("02_Pages", true))
        {
            pageKey.SetValue("MUIVerb", "Pages & Tools");
            pageKey.SetValue("SubCommands", "");
            using var pageShell = pageKey.CreateSubKey("shell", true);
            AddMenuItem(pageShell, "01_Split", "Split into single pages", $"\"{exePath}\" --action pdf-split \"%1\"");
            AddMenuItem(pageShell, "02_Rotate", "Rotate pages 90° Clockwise", $"\"{exePath}\" --action pdf-rotate --angle 90 \"%1\"");
            AddMenuItem(pageShell, "03_Tools", "PDF tools (Split, Extract, Rotate)...", $"\"{exePath}\" --action pdf-tools \"%1\"");
        }

        // Submenu 3: MORE ACTIONS
        using (var moreKey = rootShell.CreateSubKey("03_More", true))
        {
            moreKey.SetValue("MUIVerb", "More actions");
            moreKey.SetValue("SubCommands", "");
            using var moreShell = moreKey.CreateSubKey("shell", true);
            AddMenuItem(moreShell, "01_CopyPath", "Copy file path", $"\"{exePath}\" --action copy-path \"%1\"");
            AddMenuItem(moreShell, "02_FileInfo", "View PDF information", $"\"{exePath}\" --action info \"%1\"");
        }
    }

    private static void AddMenuItem(RegistryKey parentShellKey, string keyName, string label, string command)
    {
        using var itemKey = parentShellKey.CreateSubKey(keyName, true);
        itemKey.SetValue("MUIVerb", label);

        using var cmdKey = itemKey.CreateSubKey("command", true);
        cmdKey.SetValue("", command);
    }

    public static void EnableDirectWin11Menu()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(Win11ClassicClsidKey, true);
            key.SetValue("", "");
        }
        catch { }
    }

    public static void DisableDirectWin11Menu()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}", false);
        }
        catch { }
    }

    private static void CleanKey(string path)
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(path, false);
        }
        catch { }
    }

    public static void Unregister()
    {
        CleanKey(ImageBaseKey);
        CleanKey(AudioBaseKey);
        CleanKey(VideoBaseKey);
        CleanKey(PdfBaseKey);

        foreach (var ext in ImageExtensions)
        {
            CleanKey($@"Software\Classes\SystemFileAssociations\{ext}\shell\QuickFile");
            CleanKey($@"Software\Classes\SystemFileAssociations\{ext}\shell\DropConvert");
        }

        foreach (var ext in AudioExtensions)
        {
            CleanKey($@"Software\Classes\SystemFileAssociations\{ext}\shell\QuickFile");
        }

        foreach (var ext in VideoExtensions)
        {
            CleanKey($@"Software\Classes\SystemFileAssociations\{ext}\shell\QuickFile");
        }

        foreach (var ext in PdfExtensions)
        {
            CleanKey($@"Software\Classes\SystemFileAssociations\{ext}\shell\QuickFile");
        }

        CleanKey(@"Software\Classes\SystemFileAssociations\image\shell\DropConvert");
        DisableDirectWin11Menu();
    }
}
