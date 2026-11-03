using System;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace QuickFile.Core;

[SupportedOSPlatform("windows")]
public static class ShellManager
{
    private const string BaseKey = @"Software\Classes\SystemFileAssociations\image\shell\QuickFile";
    private const string Win11ClassicClsidKey = @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32";
    private static readonly string[] SupportedExtensions = { ".png", ".jpg", ".jpeg", ".webp", ".bmp" };

    public static bool IsRegistered(string? exePath = null)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(BaseKey);
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

        // 1. Primary registration under SystemFileAssociations\image
        RegisterPath(BaseKey, exePath);

        // 2. Fallback per-extension registrations for maximum compatibility
        foreach (var ext in SupportedExtensions)
        {
            string extKey = $@"Software\Classes\SystemFileAssociations\{ext}\shell\QuickFile";
            RegisterPath(extKey, exePath);
        }

        // 3. Enable direct Windows 11 right-click access
        EnableDirectWin11Menu();
    }

    private static void RegisterPath(string regPath, string exePath)
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(regPath, false);
        }
        catch { }

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

    public static void Unregister()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(BaseKey, false);
        }
        catch { }

        // Also clean legacy DropConvert key if present
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\SystemFileAssociations\image\shell\DropConvert", false);
        }
        catch { }

        foreach (var ext in SupportedExtensions)
        {
            string extKey = $@"Software\Classes\SystemFileAssociations\{ext}\shell\QuickFile";
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(extKey, false);
            }
            catch { }

            string legacyExtKey = $@"Software\Classes\SystemFileAssociations\{ext}\shell\DropConvert";
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(legacyExtKey, false);
            }
            catch { }
        }

        DisableDirectWin11Menu();
    }
}
