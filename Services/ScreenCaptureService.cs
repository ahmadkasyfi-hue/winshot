// File: Services/ScreenCaptureService.cs
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using WinShot.Interop;

namespace WinShot.Services;

/// <summary>
/// Virtual-screen capture via GDI BitBlt. GDI is chosen over DWM/DXGI because
/// it's lower-overhead, doesn't require a graphics device, and captures all
/// monitors in a single shot when CAPTUREBLT is combined with SRCCOPY.
///
/// Caveats:
///  - Per-Monitor V2 DPI awareness MUST be set via app.manifest, otherwise
///    BitBlt returns virtualized/scaled pixels on high-DPI monitors.
///  - Does NOT capture DRM-protected content (Netflix etc.) — BitBlt will
///    return black for those regions. That's expected.
/// </summary>
public sealed class ScreenCaptureService : IScreenCaptureService
{
    public Int32Rect VirtualScreenBounds
    {
        get
        {
            int x = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
            int y = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);
            int w = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN);
            int h = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN);
            return new Int32Rect(x, y, w, h);
        }
    }

    public BitmapSource CaptureVirtualScreen()
    {
        var bounds = VirtualScreenBounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            throw new InvalidOperationException("Virtual screen has zero area — no monitors detected.");

        IntPtr hdcScreen = IntPtr.Zero;
        IntPtr hdcMem = IntPtr.Zero;
        IntPtr hBitmap = IntPtr.Zero;
        IntPtr hOld = IntPtr.Zero;

        try
        {
            hdcScreen = NativeMethods.GetWindowDC(NativeMethods.GetDesktopWindow());
            if (hdcScreen == IntPtr.Zero) throw new InvalidOperationException("GetWindowDC failed.");

            hdcMem = NativeMethods.CreateCompatibleDC(hdcScreen);
            if (hdcMem == IntPtr.Zero) throw new InvalidOperationException("CreateCompatibleDC failed.");

            hBitmap = NativeMethods.CreateCompatibleBitmap(hdcScreen, bounds.Width, bounds.Height);
            if (hBitmap == IntPtr.Zero) throw new InvalidOperationException("CreateCompatibleBitmap failed.");

            hOld = NativeMethods.SelectObject(hdcMem, hBitmap);

            // SRCCOPY | CAPTUREBLT: include layered/transparent windows and cursors' layered overlays.
            bool ok = NativeMethods.BitBlt(
                hdcMem, 0, 0, bounds.Width, bounds.Height,
                hdcScreen, bounds.X, bounds.Y,
                NativeMethods.SRCCOPY | NativeMethods.CAPTUREBLT);
            if (!ok) throw new InvalidOperationException("BitBlt failed.");

            // Convert the HBITMAP into a freezable WPF BitmapSource. We MUST
            // Freeze() so the bitmap can safely cross threads (editor might
            // marshal onto a background render thread).
            var bitmap = Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                palette: IntPtr.Zero,
                sourceRect: Int32Rect.Empty,
                sizeOptions: BitmapSizeOptions.FromEmptyOptions());
            bitmap.Freeze();
            return bitmap;
        }
        finally
        {
            if (hOld != IntPtr.Zero)     NativeMethods.SelectObject(hdcMem, hOld);
            if (hBitmap != IntPtr.Zero)  NativeMethods.DeleteObject(hBitmap);
            if (hdcMem != IntPtr.Zero)   NativeMethods.DeleteDC(hdcMem);
            if (hdcScreen != IntPtr.Zero) NativeMethods.ReleaseDC(NativeMethods.GetDesktopWindow(), hdcScreen);
        }
    }

    public BitmapSource Crop(BitmapSource source, Int32Rect region)
    {
        ArgumentNullException.ThrowIfNull(source);

        // Clamp to the source bounds — users dragging near an edge can overshoot by 1px.
        int x = Math.Clamp(region.X, 0, source.PixelWidth);
        int y = Math.Clamp(region.Y, 0, source.PixelHeight);
        int w = Math.Clamp(region.Width, 1, source.PixelWidth - x);
        int h = Math.Clamp(region.Height, 1, source.PixelHeight - y);

        var cropped = new CroppedBitmap(source, new Int32Rect(x, y, w, h));
        cropped.Freeze();
        return cropped;
    }
}
