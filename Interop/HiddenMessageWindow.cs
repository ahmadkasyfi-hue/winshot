// File: Interop/HiddenMessageWindow.cs
using System;
using System.Windows;
using System.Windows.Interop;

namespace WinShot.Interop;

/// <summary>
/// Creates an invisible top-level WPF window purely to own an HWND that can
/// receive WM_HOTKEY messages. A message-only window (HWND_MESSAGE) can't
/// receive WM_HOTKEY, which is why we use a hidden zero-size visible window.
/// </summary>
internal sealed class HiddenMessageWindow : IDisposable
{
    private readonly Window _window;
    private readonly HwndSource _source;

    public IntPtr Handle { get; }

    /// <summary>Raised for every native window message; subscribers can route WM_HOTKEY.</summary>
    public event HwndSourceHook? MessageHook
    {
        add    { if (value is not null) _source.AddHook(value); }
        remove { if (value is not null) _source.RemoveHook(value); }
    }

    public HiddenMessageWindow()
    {
        _window = new Window
        {
            Width = 0,
            Height = 0,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = false,
            Visibility = Visibility.Hidden,
            Opacity = 0,
            AllowsTransparency = true,
            Background = null,
            Title = "WinShot.HiddenMessageWindow",
        };
        _window.Show();
        _window.Hide();

        var helper = new WindowInteropHelper(_window);
        Handle = helper.Handle;
        _source = HwndSource.FromHwnd(Handle)
            ?? throw new InvalidOperationException("Failed to acquire HwndSource for hidden window.");
    }

    public void Dispose()
    {
        _source.Dispose();
        _window.Close();
    }
}
