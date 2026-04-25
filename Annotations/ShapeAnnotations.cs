// File: Annotations/ShapeAnnotations.cs
using System;
using System.Windows;
using System.Windows.Media;

namespace WinShot.Annotations;

// Simple vector shapes. Each one is a thin override of Render; geometry is
// computed from Start/End on the fly. HitTest uses edge-proximity for
// outlined shapes (Rect/Ellipse) and segment-distance for 1-D shapes
// (Line/Arrow/Highlighter) — we do NOT hit the empty interior of an
// outlined rectangle, because that would grab clicks the user meant for
// whatever's behind it.

/// <summary>
/// Slop, in image pixels, around outlined shapes when hit-testing. Chosen to
/// feel forgiving at typical stroke thicknesses without swallowing nearby
/// clicks.
/// </summary>
internal static class HitTestConstants
{
    public const double EdgeSlop = 6.0;
}

public sealed class RectangleAnnotation : Annotation
{
    public override void Render(DrawingContext dc, Size imageSize)
    {
        var pen = new Pen(new SolidColorBrush(Color), Thickness);
        pen.Freeze();
        dc.DrawRectangle(brush: null, pen, Normalize(Start, End));
    }

    public override bool HitTest(Point p)
    {
        // Outlined — hit only near the four edges, not the interior.
        var r = Normalize(Start, End);
        double slop = Math.Max(HitTestConstants.EdgeSlop, Thickness / 2 + 2);
        var outer = Rect.Inflate(r, slop, slop);
        var inner = Rect.Inflate(r, -slop, -slop);
        if (inner.Width < 0 || inner.Height < 0) return outer.Contains(p);
        return outer.Contains(p) && !inner.Contains(p);
    }

    public override Annotation WithEndpoints(Point start, Point end) =>
        new RectangleAnnotation { Start = start, End = end, Color = Color, Thickness = Thickness };

    internal static Rect Normalize(Point a, Point b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
}

public sealed class EllipseAnnotation : Annotation
{
    public override void Render(DrawingContext dc, Size imageSize)
    {
        var pen = new Pen(new SolidColorBrush(Color), Thickness);
        pen.Freeze();
        var r = RectangleAnnotation.Normalize(Start, End);
        dc.DrawEllipse(brush: null, pen, new Point(r.X + r.Width / 2, r.Y + r.Height / 2), r.Width / 2, r.Height / 2);
    }

    public override bool HitTest(Point p)
    {
        // Normalized radial distance — inside the outline if |d| ∈ [1-tol, 1+tol].
        var r = RectangleAnnotation.Normalize(Start, End);
        if (r.Width < 1 || r.Height < 1) return false;
        double cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
        double rx = r.Width / 2, ry = r.Height / 2;
        double nx = (p.X - cx) / rx, ny = (p.Y - cy) / ry;
        double d2 = nx * nx + ny * ny;
        // Convert slop (pixels) to normalized units via the smaller radius
        // so the tolerance band is roughly uniform in screen space.
        double slopPx = Math.Max(HitTestConstants.EdgeSlop, Thickness / 2 + 2);
        double slopN = slopPx / Math.Min(rx, ry);
        return d2 >= (1 - slopN) * (1 - slopN) && d2 <= (1 + slopN) * (1 + slopN);
    }

    public override Annotation WithEndpoints(Point start, Point end) =>
        new EllipseAnnotation { Start = start, End = end, Color = Color, Thickness = Thickness };
}

public sealed class LineAnnotation : Annotation
{
    public override void Render(DrawingContext dc, Size imageSize)
    {
        var pen = new Pen(new SolidColorBrush(Color), Thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        pen.Freeze();
        dc.DrawLine(pen, Start, End);
    }

    public override bool HitTest(Point p) =>
        Geometry2D.DistanceToSegment(p, Start, End) <= Math.Max(HitTestConstants.EdgeSlop, Thickness / 2 + 2);

    public override Annotation WithEndpoints(Point start, Point end) =>
        new LineAnnotation { Start = start, End = end, Color = Color, Thickness = Thickness };
}

/// <summary>
/// Line with a solid arrowhead. The head is a triangle anchored at <see cref="Annotation.End"/>,
/// sized proportionally to Thickness so thicker strokes get proportionally larger heads.
/// </summary>
public sealed class ArrowAnnotation : Annotation
{
    public override void Render(DrawingContext dc, Size imageSize)
    {
        var brush = new SolidColorBrush(Color);
        brush.Freeze();
        var pen = new Pen(brush, Thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        pen.Freeze();

        // Head dimensions scale with stroke thickness.
        double headLen = Math.Max(12, Thickness * 4);
        double headHalf = headLen * 0.6;

        var dx = End.X - Start.X;
        var dy = End.Y - Start.Y;
        var len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1) return;

        // Unit vectors along and perpendicular to the line.
        double ux = dx / len, uy = dy / len;
        double px = -uy, py = ux;

        // Shaft stops at the base of the arrowhead so the head isn't overstroked.
        var shaftEnd = new Point(End.X - ux * headLen, End.Y - uy * headLen);
        dc.DrawLine(pen, Start, shaftEnd);

        var tip = End;
        var left = new Point(End.X - ux * headLen + px * headHalf, End.Y - uy * headLen + py * headHalf);
        var right = new Point(End.X - ux * headLen - px * headHalf, End.Y - uy * headLen - py * headHalf);

        var head = new StreamGeometry();
        using (var g = head.Open())
        {
            g.BeginFigure(tip, isFilled: true, isClosed: true);
            g.LineTo(left, isStroked: false, isSmoothJoin: false);
            g.LineTo(right, isStroked: false, isSmoothJoin: false);
        }
        head.Freeze();
        dc.DrawGeometry(brush, pen: null, head);
    }

    public override bool HitTest(Point p)
    {
        // Segment hit — good enough, and the arrowhead is at End so the
        // segment hit already covers the visually-thickest part.
        double headLen = Math.Max(12, Thickness * 4);
        double slop = Math.Max(HitTestConstants.EdgeSlop, Math.Max(Thickness / 2 + 2, headLen * 0.4));
        return Geometry2D.DistanceToSegment(p, Start, End) <= slop;
    }

    public override Annotation WithEndpoints(Point start, Point end) =>
        new ArrowAnnotation { Start = start, End = end, Color = Color, Thickness = Thickness };
}

/// <summary>
/// Highlighter = thick, semi-transparent line, drawn BELOW other annotations.
/// </summary>
public sealed class HighlighterAnnotation : Annotation
{
    public HighlighterAnnotation()
    {
        Color = Colors.Yellow;
        Thickness = 18;
    }

    public override void Render(DrawingContext dc, Size imageSize)
    {
        // ~40% alpha yellow, tuned to feel like a real highlighter marker.
        var c = Color;
        c.A = 0x66;
        var pen = new Pen(new SolidColorBrush(c), Thickness) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
        pen.Freeze();
        dc.DrawLine(pen, Start, End);
    }

    public override bool HitTest(Point p) =>
        // Highlighter is thick and filled-looking — hit anywhere within the stroke.
        Geometry2D.DistanceToSegment(p, Start, End) <= Thickness / 2;

    public override Annotation WithEndpoints(Point start, Point end) =>
        new HighlighterAnnotation { Start = start, End = end, Color = Color, Thickness = Thickness };
}

/// <summary>
/// Distance helpers kept local — we don't need a full geometry lib, just
/// point-to-segment for 1-D shape hit testing.
/// </summary>
internal static class Geometry2D
{
    public static double DistanceToSegment(Point p, Point a, Point b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double len2 = dx * dx + dy * dy;
        if (len2 < 1e-6) return (p - a).Length;
        // Project p onto the line, clamp t to [0,1] so we measure to the segment.
        double t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2;
        t = Math.Clamp(t, 0, 1);
        double px = a.X + t * dx, py = a.Y + t * dy;
        double ex = p.X - px, ey = p.Y - py;
        return Math.Sqrt(ex * ex + ey * ey);
    }
}
