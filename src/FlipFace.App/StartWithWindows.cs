using Microsoft.Win32;

namespace FlipFace.App;

/// <summary>The per-user "Run" registry entry that starts FlipFace in the tray when you sign in.</summary>
public static class StartWithWindows
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "FlipFace";

    /// <summary>Command-line flag that starts FlipFace without showing its window.</summary>
    public const string TrayFlag = "--tray";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string;
        }
        set
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (value)
                key.SetValue(ValueName, $"\"{Environment.ProcessPath}\" {TrayFlag}");
            else
                key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
