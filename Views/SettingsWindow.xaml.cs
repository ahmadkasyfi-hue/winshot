// File: Views/SettingsWindow.xaml.cs
using System;
using System.Windows;
using System.Windows.Controls;
using WinShot.Services;

namespace WinShot.Views;

public partial class SettingsWindow : Window
{
    private readonly ISettingsService _settings;

    public SettingsWindow(ISettingsService settings)
    {
        InitializeComponent();
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));

        // Load current values into controls.
        var s = _settings.Current;
        SaveDirBox.Text = s.SaveDirectory;
        FileNameBox.Text = s.FileNameTemplate;
        FormatBox.SelectedIndex = s.DefaultFormat.Equals("jpg", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        QualitySlider.Value = s.JpegQuality;
        QualityText.Text = s.JpegQuality.ToString();
        RevealCheck.IsChecked = s.RevealInExplorerAfterSave;

        QualitySlider.ValueChanged += (_, e) => QualityText.Text = ((int)e.NewValue).ToString();
    }

    private void OnBrowse(object sender, RoutedEventArgs e)
    {
        // WPF lacks a built-in folder picker that fits modern UX.
        // Microsoft.Win32.OpenFolderDialog is available from .NET 8 — no extra NuGet needed.
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Choose where to save screenshots",
            InitialDirectory = SaveDirBox.Text,
        };
        if (dlg.ShowDialog(this) == true)
            SaveDirBox.Text = dlg.FolderName;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        // Validate & commit.
        var dir = SaveDirBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(dir))
        {
            MessageBox.Show(this, "Please choose a save directory.", "WinShot",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            System.IO.Directory.CreateDirectory(dir);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                $"Can't create or access '{dir}':\n{ex.Message}",
                "WinShot", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        _settings.Current.SaveDirectory = dir;
        _settings.Current.FileNameTemplate = string.IsNullOrWhiteSpace(FileNameBox.Text)
            ? "WinShot_{timestamp:yyyyMMdd_HHmmss}"
            : FileNameBox.Text.Trim();

        var fmtItem = (ComboBoxItem)FormatBox.SelectedItem;
        _settings.Current.DefaultFormat = (string)fmtItem.Tag;
        _settings.Current.JpegQuality = (int)QualitySlider.Value;
        _settings.Current.RevealInExplorerAfterSave = RevealCheck.IsChecked == true;

        try
        {
            _settings.Save();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Failed to save settings:\n" + ex.Message,
                "WinShot", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        DialogResult = true;
        Close();
    }
}
