using System;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace eScratch;
public static class LoginStartup
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "eScratch.Wpf";
    public static string Command(string executable, string assembly) =>
        "\"" + executable + "\"" + (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase) ? " \"" + assembly + "\"" : "") + " --login";
    public static bool Enabled
    {
        get { using var key = Registry.CurrentUser.OpenSubKey(RunKey); return key?.GetValue(ValueName) is string value && value.Length > 0; }
    }
    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, true);
        if (enabled) key.SetValue(ValueName, Command(Environment.ProcessPath ?? throw new InvalidOperationException("Application path is unavailable."), Assembly.GetExecutingAssembly().Location));
        else key.DeleteValue(ValueName, false);
    }
}
