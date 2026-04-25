// File: Services/IScreenCaptureService.cs
using System.Windows;
using System.Windows.Media.Imaging;

namespace WinShot.Services;

/// <summary>
/// Abstraction over screen capture so the editor / region UI can be tested
/// against a fake that returns a canned <see cref="BitmapSource"/>.
/// </summary>
public interface IScreenCaptureService
{
    /// <summary>Union bounds of all monitors, in physical pixels.</summary>
    Int32Rect VirtualScreenBounds { get; }

    /// <summary>Captures every monitor into a single bitmap matching <see cref="VirtualScreenBounds"/>.</summary>
    BitmapSource CaptureVirtualScreen();

    /// <summary>Crops an in-memory bitmap to <paramref name="region"/>.</summary>
    /// <remarks>Coordinates are in physical pixels relative to <see cref="VirtualScreenBounds"/>.</remarks>
    BitmapSource Crop(BitmapSource source, Int32Rect region);
}
