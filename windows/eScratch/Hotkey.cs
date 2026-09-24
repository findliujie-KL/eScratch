using System;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace eScratch;
public sealed class Hotkey : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr handle, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr handle, int id);
    private readonly HwndSource source;
    private readonly Action toggle;
    private int activeId;
    private string? registered;
    public Hotkey(IntPtr handle, Action toggle) { source = HwndSource.FromHwnd(handle); this.toggle = toggle; source.AddHook(Hook); }
    public static KeyGesture Parse(string text) => (KeyGesture)new KeyGestureConverter().ConvertFromInvariantString(text.Replace("Control", "Ctrl"))!;
    public bool Register(string text)
    {
        if (registered == text) return true;
        KeyGesture gesture;
        try { gesture = Parse(text); } catch { return false; }
        if (gesture.Modifiers == ModifierKeys.None) return false;
        uint modifiers = 0x4000;
        if (gesture.Modifiers.HasFlag(ModifierKeys.Alt)) modifiers |= 1;
        if (gesture.Modifiers.HasFlag(ModifierKeys.Control)) modifiers |= 2;
        if (gesture.Modifiers.HasFlag(ModifierKeys.Shift)) modifiers |= 4;
        if (gesture.Modifiers.HasFlag(ModifierKeys.Windows)) modifiers |= 8;
        var nextId = activeId == 1 ? 2 : 1;
        if (!RegisterHotKey(source.Handle, nextId, modifiers, (uint)KeyInterop.VirtualKeyFromKey(gesture.Key))) return false;
        if (activeId != 0) UnregisterHotKey(source.Handle, activeId);
        activeId = nextId;
        registered = text;
        return true;
    }
    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wp, IntPtr lp, ref bool handled)
    { if (msg == 0x312 && wp.ToInt32() == activeId) { handled = true; toggle(); } return IntPtr.Zero; }
    public void Dispose() { if (activeId != 0) UnregisterHotKey(source.Handle, activeId); source.RemoveHook(Hook); }
}
