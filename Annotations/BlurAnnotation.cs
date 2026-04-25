// File: Annotations/BlurAnnotation.cs
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WinShot.Annotations;

/// <summary>
/// Pixelates a rectangular region of the underlying source image. We implement
/// redaction as pixelation (not gaussian blur) because real blur can sometimes
/// be reversed — mosaic pixelation at this tile size is safer for hiding
/// passwords, emails, etc.
/// </summary>
public sealed class BlurAnnotation : Annotation
{
    public BitmapSource? SourceImage { get; init; }
    public int TileSize { get; init; } = 14;

    public override void Render(DrawingContext dc, Size imageSize)
    {
        if (SourceImage is null) return;
        var rect = RectangleAnnotation.Normalize(Start, End);
        if (rect.Width < 1 || rect.Height < 1) return;

        // Clamp to image bounds.
        rect = Rect.Intersect(rect, new Rect(0, 0, imageSize.Width, imageSize.Height));
        if (rect.IsEmpty) return;

        // 1. Crop the source to the region of interest.
        var sourceRect = new Int32Rect(
            (int)rect.X, (int)rect.Y,
            System.Math.Max(1, (int)rect.Width),
            System.Math.Max(1, (int)rect.Height));
        var cropped = new CroppedBitmap(SourceImage, sourceRect);

        // 2. Downsample to a tiny thumbnail...
        int downW = System.Math.Max(1, (int)(rect.Width  / TileSize));
        int downH = System.Math.Max(1, (int)(rect.Height / TileSize));
        var scaled = new TransformedBitmap(cropped,
            new ScaleTransform((double)downW / cropped.PixelWidth, (double)downH / cropped.PixelHeight));

        // 3. ...then draw it back up at the original size with nearest-neighbor,
        //    which produces the classic mosaic pixelation effect.
        //    RenderOptions must be set on the bitmap itself (not the DC),
        //    since DrawingContext is not a DependencyObject.
        RenderOptions.SetBitmapScalingMode(scaled, BitmapScalingMode.NearestNeighbor);
        dc.DrawImage(scaled, rect);
    }

    /// <summary>
    /// Opaque region — hit-test is interior containment. This is the one shape
    /// where clicking the middle of the rectangle counts as a hit.
    /// </summary>
    public override bool HitTest(Point p) =>
        RectangleAnnotation.Normalize(Start, End).Contains(p);

    public override Annotation WithEndpoints(Point start, Point end) =>
        new BlurAnnotation
        {
            Start = start,
            End = end,
            Color = Color,
            Thickness = Thickness,
            SourceImage = SourceImage,
            TileSize = TileSize,
        };
}
