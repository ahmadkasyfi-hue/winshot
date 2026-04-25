// File: Services/IClipboardService.cs
using System.Windows.Media.Imaging;

namespace WinShot.Services;

public interface IClipboardService
{
    /// <summary>Puts a bitmap on the Windows clipboard in PNG + DIB form so most apps can paste it.</summary>
    void SetImage(BitmapSource image);
}
