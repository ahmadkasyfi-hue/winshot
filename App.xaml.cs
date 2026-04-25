// File: App.xaml.cs
using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WinShot.Interop;
using WinShot.Services;
using WinShot.Views;

namespace WinShot;

/// <summary>
/// Application entry point. Wires DI, loads settings, registers the global
/// hotkey, and materializes the tray icon. There is no "main window" — the
/// app lives in the tray and shows the editor only on demand.
/// </summary>
public partial class App : Application
{
    // Single-instance guard — duplicate hotkey registrations would fight.
    private static Mutex? _instanceMutex;

    private IServiceProvider? _services;
    private HotKeyManager? _hotKeyManager;
    private HiddenMessageWindow? _messageWindow;
    private TaskbarIcon? _trayIcon;
    private volatile bool _captureInProgress;

    // Default hotkey: Ctrl+Shift+S.
    private const uint DefaultModifiers = HotKeyManager.MOD_CONTROL | HotKeyManager.MOD_SHIFT;
    private const uint DefaultVirtualKey = 0x53; // 'S'

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        const string mutexName = @"Global\WinShot.SingleInstance.{6B9E5D3A-5A9F-4E2E-9E4E-1A2B3C4D5E6F}";
        _instanceMutex = new Mutex(initiallyOwned: true, name: mutexName, out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "WinShot is already running — check the system tray (bottom-right) or press Ctrl+Shift+S.",
                "WinShot", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        _services = ConfigureServices();

        var logger = _services.GetRequiredService<ILogger<App>>();

        DispatcherUnhandledException += (_, ex) =>
        {
            logger.LogError(ex.Exception, "Unhandled dispatcher exception");
            MessageBox.Show(ex.Exception.ToString(), "WinShot – Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            ex.Handled = true;
        };

        // Hidden HWND for WM_HOTKEY (message-only windows can't receive it).
        _messageWindow = new HiddenMessageWindow();
        _hotKeyManager = new HotKeyManager(_messageWindow.Handle);
        _hotKeyManager.HotKeyPressed += OnHotKeyPressed;

        try
        {
            _hotKeyManager.Register(DefaultModifiers, DefaultVirtualKey);
            logger.LogInformation("Global hotkey registered: Ctrl+Shift+S");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to register global hotkey");
            MessageBox.Show(
                "Could not register Ctrl+Shift+S — another app may own it.\n\nRight-click the WinShot tray icon to capture manually.",
                "WinShot", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        InitializeTrayIcon();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _hotKeyManager?.Dispose();
        _messageWindow?.Dispose();

        try { _instanceMutex?.ReleaseMutex(); } catch (ApplicationException) { }
        _instanceMutex?.Dispose();

        base.OnExit(e);
    }

    private IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddLogging(b =>
        {
            b.AddDebug();
            b.SetMinimumLevel(LogLevel.Debug);
        });

        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IScreenCaptureService, ScreenCaptureService>();
        services.AddSingleton<IClipboardService, ClipboardService>();

        return services.BuildServiceProvider();
    }

    // ----- Tray icon -----

    private void InitializeTrayIcon()
    {
        _trayIcon = (TaskbarIcon)FindResource("WinShotTray");

        // Wire each menu item by its Tag. Tags are set in Views/TrayIconHost.xaml.
        foreach (var item in _trayIcon.ContextMenu.Items.OfType<MenuItem>())
        {
            switch ((item.Tag as string))
            {
                case "capture":  item.Click += (_, _) => TriggerCapture(); break;
                case "settings": item.Click += (_, _) => ShowSettings(); break;
                case "about":    item.Click += (_, _) => ShowAbout(); break;
                case "exit":     item.Click += (_, _) => Shutdown(); break;
            }
        }

        // Double-clicking the tray icon triggers a capture for quick access.
        _trayIcon.TrayMouseDoubleClick += (_, _) => TriggerCapture();
    }

    private void ShowSettings()
    {
        var settings = _services!.GetRequiredService<ISettingsService>();
        var dlg = new SettingsWindow(settings);
        dlg.ShowDialog();
    }

    private void ShowAbout()
    {
        MessageBox.Show(
            "WinShot 0.2.0\n\n" +
            "Ctrl+Shift+S or double-click the tray icon to capture a region.\n" +
            "Right-click the tray icon for settings.\n\n" +
            "Keyboard shortcuts inside the editor:\n" +
            "  S = Select      A = Arrow       R = Rectangle    E = Ellipse\n" +
            "  L = Line        T = Text        H = Highlighter  B = Blur\n" +
            "  Delete = remove selected shape\n" +
            "  Ctrl+Z = Undo   Ctrl+C = Copy   Ctrl+S = Save    Ctrl+Enter = Save & Copy\n",
            "About WinShot",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    // ----- Capture flow -----

    private void OnHotKeyPressed(object? sender, EventArgs e) => TriggerCapture();

    private async void TriggerCapture()
    {
        if (_captureInProgress) return;
        _captureInProgress = true;

        try
        {
            var capture = _services!.GetRequiredService<IScreenCaptureService>();
            var logger  = _services!.GetRequiredService<ILogger<App>>();

            BitmapSource fullScreen;
            try { fullScreen = capture.CaptureVirtualScreen(); }
            catch (Exception ex)
            {
                logger.LogError(ex, "Full-screen capture failed");
                return;
            }

            var select = new RegionSelectWindow(fullScreen, capture.VirtualScreenBounds);
            if (select.ShowDialog() != true || select.SelectedRegion is null)
            {
                logger.LogDebug("Region selection cancelled");
                return;
            }

            var cropped = capture.Crop(fullScreen, select.SelectedRegion.Value);

            var editor = new EditorWindow(
                cropped,
                _services!.GetRequiredService<IClipboardService>(),
                _services!.GetRequiredService<ISettingsService>(),
                _services!.GetRequiredService<ILogger<EditorWindow>>());
            editor.Show();

            await System.Threading.Tasks.Task.Yield();
        }
        finally
        {
            _captureInProgress = false;
        }
    }
}
