// File: Interop/HotKeyManager.cs
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace WinShot.Interop;

/// <summary>
/// Thin, disposable wrapper over <c>RegisterHotKey</c>/<c>UnregisterHotKey</c>.
/// Raises <see cref="HotKeyPressed"/> on the UI thread whenever a registered
/// hotkey is pressed anywhere on the system.
///
/// NOTE: Registration / unregistration MUST happen on the thread that owns
/// the target HWND (per Win32 contract). We rely on being constructed on the
/// UI thread from <c>App.OnStartup</c>.
/// </summary>
internal sealed class HotKeyManager : IDisposable
{
    // Modifier flags accepted by RegisterHotKey.
    public const uint MOD_ALT      = 0x0001;
    public const uint MOD_CONTROL  = 0x0002;
    public const uint MOD_SHIFT    = 0x0004;
    public const uint MOD_WIN      = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;

    private readonly IntPtr _hwnd;
    private readonly HwndSource _source;
    private readonly Dictionary<int, (uint mods, uint vk)> _registered = new();
    private int _nextId = 0x9000; // arbitrary app-private ID range
    private bool _disposed;

    public event EventHandler? HotKeyPressed;

    public HotKeyManager(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            throw new ArgumentException("HWND required.", nameof(hwnd));

        _hwnd = hwnd;
        _source = HwndSource.FromHwnd(hwnd)
            ?? throw new InvalidOperationException("HwndSource not available for hotkey HWND.");
        _source.AddHook(WndProc);
    }

    /// <summary>Registers a hotkey. Throws <see cref="Win32Exception"/> if already owned by another app.</summary>
    public int Register(uint modifiers, uint virtualKey)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        int id = _nextId++;
        // MOD_NOREPEAT ensures key repeat doesn't flood us with events when held.
        if (!NativeMethods.RegisterHotKey(_hwnd, id, modifiers | MOD_NOREPEAT, virtualKey))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(),
                $"RegisterHotKey failed for modifiers=0x{modifiers:X} vk=0x{virtualKey:X}.");
        }

        _registered[id] = (modifiers, virtualKey);
        return id;
    }

    public void Unregister(int id)
    {
        if (_registered.Remove(id))
        {
            NativeMethods.UnregisterHotKey(_hwnd, id);
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && _registered.ContainsKey(wParam.ToInt32()))
        {
            HotKeyPressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Unregister everything; ignore errors on shutdown.
        foreach (var id in new List<int>(_registered.Keys))
        {
            try { NativeMethods.UnregisterHotKey(_hwnd, id); } catch { /* shutdown */ }
        }
        _registered.Clear();

        _source.RemoveHook(WndProc);
    }
}
