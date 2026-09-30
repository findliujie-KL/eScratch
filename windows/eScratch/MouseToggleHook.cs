using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Threading;

namespace eScratch;

// A low-level hook is required because RegisterHotKey accepts keyboard keys only.
public sealed class MouseToggleHook : IDisposable
{
    private const int WhMouseLl = 14, MiddleDown = 0x207, MiddleUp = 0x208;
    private readonly Dispatcher dispatcher;
    private readonly Action toggle;
    private readonly HookProc callback;
    private IntPtr hook;
    private ModifierKeys modifiers;
    private bool enabled, swallowUp;
    public bool Suspended { get; set; }

    private delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int id, HookProc callback, IntPtr module, uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);

    public MouseToggleHook(Dispatcher dispatcher, Action toggle)
    { this.dispatcher = dispatcher; this.toggle = toggle; callback = OnMouse; }

    public static bool TryParse(string? text, out ModifierKeys keys)
    {
        keys = ModifierKeys.None;
        if (string.IsNullOrWhiteSpace(text)) return true;
        var parts = text!.Split('+');
        if (parts[parts.Length - 1] != "MiddleClick") return false;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            ModifierKeys key;
            switch (parts[i])
            {
                case "Control": key = ModifierKeys.Control; break;
                case "Shift": key = ModifierKeys.Shift; break;
                case "Alt": key = ModifierKeys.Alt; break;
                case "Windows": key = ModifierKeys.Windows; break;
                default: return false;
            }
            if ((keys & key) != 0) return false;
            keys |= key;
        }
        return true;
    }

    public void Register(string? gesture)
    {
        if (!TryParse(gesture, out var next)) throw new ArgumentException("Invalid mouse gesture.", nameof(gesture));
        if (string.IsNullOrEmpty(gesture)) { enabled = false; swallowUp = false; DisposeHook(); return; }
        if (hook == IntPtr.Zero)
        {
            hook = SetWindowsHookEx(WhMouseLl, callback, GetModuleHandle(null), 0);
            if (hook == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not register the mouse toggle.");
        }
        modifiers = next;
        enabled = true;
    }

    private static bool Down(int key) => (GetAsyncKeyState(key) & 0x8000) != 0;
    private static ModifierKeys CurrentModifiers()
    {
        var keys = ModifierKeys.None;
        if (Down(0x11)) keys |= ModifierKeys.Control;
        if (Down(0x10)) keys |= ModifierKeys.Shift;
        if (Down(0x12)) keys |= ModifierKeys.Alt;
        if (Down(0x5B) || Down(0x5C)) keys |= ModifierKeys.Windows;
        return keys;
    }
    private IntPtr OnMouse(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0 && enabled)
        {
            if (message.ToInt32() == MiddleDown && !Suspended && CurrentModifiers() == modifiers)
            {
                swallowUp = true;
                dispatcher.BeginInvoke(toggle);
                return (IntPtr)1;
            }
            if (message.ToInt32() == MiddleUp && swallowUp)
            {
                swallowUp = false;
                return (IntPtr)1;
            }
        }
        return CallNextHookEx(hook, code, message, data);
    }
    private void DisposeHook() { if (hook != IntPtr.Zero) { UnhookWindowsHookEx(hook); hook = IntPtr.Zero; } }
    public void Dispose() { enabled = false; DisposeHook(); }
}
