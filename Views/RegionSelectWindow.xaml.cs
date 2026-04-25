// File: Views/RegionSelectWindow.xaml.cs
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WinShot.Views;

/// <summary>
/// Full-virtual-screen overlay window that lets the user drag a selection
/// rectangle. Returns its pixel-space <see cref="SelectedRegion"/> to the
/// caller via <c>DialogResult</c>.
///
/// Coordinate systems:
///   - The window is positioned in WPF device-independent units (DIPs).
///   - We need the final selection in PHYSICAL pixels so it indexes the
///     already-captured bitmap. We do the conversion in <see cref="GetPhysicalSelection"/>.
/// </summary>
public partial class RegionSelectWindow : Window
{
    private readonly BitmapSource _fullScreen;
    private readonly Int32Rect _virtualScreenPx;

    private Point? _dragStart;   // in window DIPs
    private Point _dragEnd;

    public Int32Rect? SelectedRegion { get; private set; }

    public RegionSelectWindow(BitmapSource fullScreen, Int32Rect virtualScreenPx)
    {
        InitializeComponent();

        _fullScreen = fullScreen ?? throw new ArgumentNullException(nameof(fullScreen));
        _virtualScreenPx = virtualScreenPx;

        BackgroundImage.Source = _fullScreen;

        // We want the window to cover the ENTIRE virtual screen. WPF places
        // windows in DIPs, and Left/Top/Width/Height below expect DIPs at 96 DPI
        // relative to the primary monitor — which is the correct reference for
        // PerMonitorV2 positioning in multi-monitor mixed-DPI setups.
        // (WPF handles per-monitor DPI internally; we just hand it physical
        // pixel values that equal DIP values because the bitmap's own DPI is
        // baked at capture-time.)
        Left   = _virtualScreenPx.X;
        Top    = _virtualScreenPx.Y;
        Width  = _virtualScreenPx.Width;
        Height = _virtualScreenPx.Height;

        // Keyboard escape.
        PreviewKeyDown += OnPreviewKeyDown;
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        Loaded += (_, _) => { Activate(); Focus(); };
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
        }
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(RootGrid);
        _dragEnd = _dragStart.Value;
        SelectionRect.Visibility = Visibility.Visible;
        SizeReadout.Visibility = Visibility.Visible;
        UpdateVisuals();
        CaptureMouse();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragStart is null) return;
        _dragEnd = e.GetPosition(RootGrid);
        UpdateVisuals();
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragStart is null) return;
        ReleaseMouseCapture();

        var (x, y, w, h) = NormalizeRect(_dragStart.Value, _dragEnd);
        if (w < 4 || h < 4)
        {
            // Too small — treat as a cancel rather than capturing a dot.
            DialogResult = false;
            Close();
            return;
        }

        SelectedRegion = GetPhysicalSelection(new Rect(x, y, w, h));
        DialogResult = true;
        Close();
    }

    private void UpdateVisuals()
    {
        if (_dragStart is null) return;

        var (x, y, w, h) = NormalizeRect(_dragStart.Value, _dragEnd);

        Canvas.SetLeft(SelectionRect, x);
        Canvas.SetTop(SelectionRect, y);
        SelectionRect.Width = w;
        SelectionRect.Height = h;

        // Punch a transparent hole in the dim veil so the user sees the original
        // pixels inside the selection rectangle.
        var geom = new CombinedGeometry(
            GeometryCombineMode.Exclude,
            new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight)),
            new RectangleGeometry(new Rect(x, y, w, h)));
        DimVeil.Clip = geom;

        // Position & content of the size readout.
        SizeText.Text = $"{(int)w} × {(int)h}";
        Canvas.SetLeft(SizeReadout, Math.Min(x + w + 8, ActualWidth - 80));
        Canvas.SetTop(SizeReadout, Math.Min(y + h + 8, ActualHeight - 24));
    }

    private static (double x, double y, double w, double h) NormalizeRect(Point a, Point b)
    {
        double x = Math.Min(a.X, b.X);
        double y = Math.Min(a.Y, b.Y);
        double w = Math.Abs(a.X - b.X);
        double h = Math.Abs(a.Y - b.Y);
        return (x, y, w, h);
    }

    /// <summary>
    /// Converts a DIP-space rect (relative to this overlay window) into
    /// physical pixels (relative to the virtual screen, which is how the
    /// source bitmap is indexed).
    /// </summary>
    private Int32Rect GetPhysicalSelection(Rect dipRect)
    {
        // PresentationSource gives us the transform from DIPs to physical px
        // for the monitor this window is currently rendered on.
        var source = PresentationSource.FromVisual(this);
        double scaleX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        double scaleY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

        // Window origin in physical pixels (virtual-screen coords).
        int px = (int)Math.Round(dipRect.X * scaleX);
        int py = (int)Math.Round(dipRect.Y * scaleY);
        int pw = (int)Math.Round(dipRect.Width * scaleX);
        int ph = (int)Math.Round(dipRect.Height * scaleY);

        return new Int32Rect(px, py, pw, ph);
    }
}
