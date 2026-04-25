// File: Services/ClipboardService.cs
using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace WinShot.Services;

/// <summary>
/// Puts a bitmap on the clipboard in multiple formats.
///
/// Why multiple formats: <see cref="Clipboard.SetImage"/> alone uses the DIB format,
/// which many apps (Teams, Slack, browsers) handle poorly — they drop the alpha
/// channel or mis-size the image. Including PNG gives modern apps a lossless,
/// properly-sized source while DIB stays as a fallback for legacy tooling (Win32,
/// classic Office) that only reads CF_DIB.
/// </summary>
public sealed class ClipboardService : IClipboardService
{
    public void SetImage(BitmapSource image)
    {
        ArgumentNullException.ThrowIfNull(image);

        // Encode once, reuse for both PNG and DIB entries.
        using var pngStream = new MemoryStream();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        encoder.Save(pngStream);
        pngStream.Position = 0;

        var data = new DataObject();
        data.SetData("PNG", pngStream, autoConvert: false);
        // Standard bitmap format for widest compatibility.
        data.SetImage(image);

        // Retry loop: the clipboard is a shared Win32 resource and another app
        // might be holding it open momentarily. 10 retries over ~200ms is plenty.
        Exception? lastError = null;
        for (int i = 0; i < 10; i++)
        {
            try
            {
                Clipboard.SetDataObject(data, copy: true);
                return;
            }
            catch (Exception ex)
            {
                lastError = ex;
                System.Threading.Thread.Sleep(20);
            }
        }

        throw new InvalidOperationException("Failed to set clipboard after multiple attempts.", lastError);
    }
}
