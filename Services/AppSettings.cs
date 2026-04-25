// File: Services/AppSettings.cs
using System;
using System.IO;

namespace WinShot.Services;

/// <summary>
/// User-configurable settings persisted as JSON under %APPDATA%\WinShot\settings.json.
/// All fields are nullable-friendly and have sensible defaults so a missing or
/// corrupted file never crashes the app at startup.
/// </summary>
public sealed class AppSettings
{
    /// <summary>Folder where silent "Save" writes annotated screenshots.</summary>
    public string SaveDirectory { get; set; } = DefaultSaveDirectory();

    /// <summary>
    /// Filename template. Supports C# <see cref="DateTime.ToString(string)"/> tokens
    /// inside <c>{timestamp:...}</c> placeholders.
    /// </summary>
    public string FileNameTemplate { get; set; } = "WinShot_{timestamp:yyyyMMdd_HHmmss}";

    /// <summary>"png" or "jpg". Informs extension and encoder selection.</summary>
    public string DefaultFormat { get; set; } = "png";

    /// <summary>JPEG quality 1..100, only used when DefaultFormat == "jpg".</summary>
    public int JpegQuality { get; set; } = 92;

    /// <summary>Open File Explorer to the file after saving.</summary>
    public bool RevealInExplorerAfterSave { get; set; } = false;

    public static string DefaultSaveDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            "WinShot");

    /// <summary>Resolves the template + format into a full file path. Ensures the target directory exists.</summary>
    public string ResolveFilePath(DateTime timestamp)
    {
        Directory.CreateDirectory(SaveDirectory);

        string name = FileNameTemplate;

        // Very small template engine: {timestamp:<format>}
        const string token = "{timestamp:";
        int i;
        while ((i = name.IndexOf(token, StringComparison.Ordinal)) >= 0)
        {
            int end = name.IndexOf('}', i + token.Length);
            if (end < 0) break;
            string fmt = name.Substring(i + token.Length, end - i - token.Length);
            string replaced;
            try { replaced = timestamp.ToString(fmt); }
            catch (FormatException) { replaced = timestamp.ToString("yyyyMMdd_HHmmss"); }
            name = name[..i] + replaced + name[(end + 1)..];
        }

        // Strip characters that would make Windows unhappy in a filename.
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');

        string ext = DefaultFormat.Equals("jpg", StringComparison.OrdinalIgnoreCase) ? ".jpg" : ".png";
        return Path.Combine(SaveDirectory, name + ext);
    }
}
