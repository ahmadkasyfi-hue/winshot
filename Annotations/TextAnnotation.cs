// File: Annotations/TextAnnotation.cs
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace WinShot.Annotations;

/// <summary>
/// Simple text label anchored at <see cref="Annotation.Start"/>. The editor uses an
/// in-place TextBox for input, then commits the string here. Text size is
/// driven by <see cref="FontSize"/>, so selection is move-only — there are no
/// resize handles. To re-edit the text itself, delete and retype (selection
/// re-entry into the TextBox is a future enhancement).
/// </summary>
public sealed class TextAnnotation : Annotation
{
    public string Text { get; init; } = string.Empty;
    public double FontSize { get; init; } = 18;
    public string FontFamily { get; init; } = "Segoe UI";
    public bool Bold { get; init; } = false;

    public override bool SupportsResize => false;

    public override void Render(DrawingContext dc, Size imageSize)
    {
        if (string.IsNullOrEmpty(Text)) return;

        var brush = new SolidColorBrush(Color);
        brush.Freeze();

        var ft = BuildFormattedText(brush);

        // Subtle dark halo so text remains legible against any background.
        var halo = new Pen(new SolidColorBrush(Color.FromArgb(0xC0, 0, 0, 0)), 3);
        halo.Freeze();
        var geometry = ft.BuildGeometry(Start);
        dc.DrawGeometry(brush: null, halo, geometry);
        dc.DrawText(ft, Start);
    }

    public override bool HitTest(Point p) => Bounds.Contains(p);

    /// <summary>
    /// Move-only: keep the End offset from Start constant so <see cref="Bounds"/>
    /// (which caches nothing) regenerates cleanly. A resize-drag on text is
    /// blocked upstream by <see cref="SupportsResize"/>, so only body-move
    /// calls reach here in practice.
    /// </summary>
    public override Annotation WithEndpoints(Point start, Point end) =>
        new TextAnnotation
        {
            Start = start,
            End = end,
            Color = Color,
            Thickness = Thickness,
            Text = Text,
            FontSize = FontSize,
            FontFamily = FontFamily,
            Bold = Bold,
        };

    /// <summary>
    /// Bounds are derived from the rendered <see cref="FormattedText"/>, not
    /// from Start/End — a text annotation's visible box is determined by font
    /// + string, not by the mouse drag that created it.
    /// </summary>
    public override Rect Bounds
    {
        get
        {
            if (string.IsNullOrEmpty(Text)) return new Rect(Start, new Size(0, 0));
            var ft = BuildFormattedText(Brushes.Black);
            // +3 halo padding on each side, matching the stroke width in Render.
            return new Rect(
                Start.X - 2, Start.Y - 2,
                ft.Width + 4, ft.Height + 4);
        }
    }

    private FormattedText BuildFormattedText(Brush brush)
    {
        var typeface = new Typeface(
            new FontFamily(FontFamily),
            FontStyles.Normal,
            Bold ? FontWeights.Bold : FontWeights.Normal,
            FontStretches.Normal);

        // FormattedText is the correct primitive for DrawingContext text;
        // TextBlock can't render directly into a DrawingContext.
        return new FormattedText(
            Text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            typeface,
            FontSize,
            brush,
            pixelsPerDip: 1.0);
    }
}
