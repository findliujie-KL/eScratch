using System;
using System.Runtime.InteropServices;
using System.Text;

namespace eScratch;

// Observe foreground changes only; never intercept keyboard or mouse input.
internal sealed class TrayFocusTracker : IDisposable
{
    private readonly WinEvent callback;
    private readonly IntPtr hook;
    private IntPtr lastApplication;
    public TrayFocusTracker()
    {
        callback = (_, _, window, _, _, _, _) => Observe(window);
        Observe(GetForegroundWindow());
        hook = SetWinEventHook(3, 3, IntPtr.Zero, callback, 0, 0, 0);
    }
    private static bool IsTray(IntPtr window)
    {
        var name = new StringBuilder(256);
        GetClassName(window, name, name.Capacity);
        return name.ToString() == "Shell_TrayWnd" || name.ToString() == "Shell_SecondaryTrayWnd"
            || name.ToString() == "NotifyIconOverflowWindow" || name.ToString() == "TopLevelWindowForOverflowXamlIsland";
    }
    private void Observe(IntPtr window)
    {
        if (window != IntPtr.Zero && !IsTray(window)) lastApplication = window;
    }
    public bool WasFocused(IntPtr window)
    {
        var foreground = GetForegroundWindow();
        Observe(foreground);
        return foreground == window || (IsTray(foreground) && lastApplication == window);
    }
    public void Dispose() { if (hook != IntPtr.Zero) UnhookWinEvent(hook); }
    private delegate void WinEvent(IntPtr hook, uint kind, IntPtr window, int objectId, int childId, uint thread, uint time);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder name, int count);
    [DllImport("user32.dll")] private static extern IntPtr SetWinEventHook(uint first, uint last, IntPtr module, WinEvent callback, uint process, uint thread, uint flags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(IntPtr hook);
}
