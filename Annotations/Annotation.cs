// File: Annotations/Annotation.cs
using System;
using System.Windows;
using System.Windows.Media;

namespace WinShot.Annotations;

/// <summary>
/// Base class for all annotation primitives. Annotations are non-destructive
/// and, once added to the editor's list, <b>immutable</b>: every edit goes
/// through the command stack (see <c>IEditCommand</c>) by constructing a new
/// annotation via <see cref="WithEndpoints"/> and replacing the list entry.
/// This keeps <c>DrawingContext</c> replay consistent — no half-edited state
/// ever reaches <see cref="Render"/>, and the on-screen preview and the
/// flattened export share one render path.
/// </summary>
public abstract class Annotation
{
    /// <summary>Stroke / primary color.</summary>
    public Color Color { get; init; } = Colors.Red;

    /// <summary>Stroke thickness in image pixels.</summary>
    public double Thickness { get; init; } = 3.0;

    /// <summary>Start point in image-pixel coordinates.</summary>
    public Point Start { get; init; }

    /// <summary>End point in image-pixel coordinates.</summary>
    public Point End { get; init; }

    /// <summary>Render this annotation into the given drawing context.</summary>
    /// <remarks>
    /// <paramref name="imageSize"/> is passed so tools like Blur can sample the
    /// underlying image correctly (they need to know the bitmap bounds).
    /// This call is the <b>single render path</b> — the on-screen preview and
    /// the exported PNG both invoke it. Never draw selection chrome here.
    /// </remarks>
    public abstract void Render(DrawingContext dc, Size imageSize);

    /// <summary>
    /// Returns true if the image-pixel point <paramref name="p"/> hits this
    /// annotation's visible geometry. Used by the editor for click-to-select.
    /// Outlined shapes hit near their outline; filled / opaque shapes (Blur,
    /// Text, Highlighter body) hit their interior.
    /// </summary>
    public abstract bool HitTest(Point p);

    /// <summary>
    /// Returns a new annotation of the same concrete type with replaced
    /// endpoints but all other state (text, source image, font, etc.)
    /// preserved. This is the single mutation primitive used by move/resize.
    /// </summary>
    public abstract Annotation WithEndpoints(Point start, Point end);

    /// <summary>
    /// Axis-aligned bounding box in image-pixel space. Used for selection
    /// visuals and bbox-style resize handles. Subclasses override when their
    /// visual extent isn't derivable from <see cref="Start"/> / <see cref="End"/>
    /// alone (e.g. <c>TextAnnotation</c>, where size comes from the font).
    /// </summary>
    public virtual Rect Bounds
    {
        get
        {
            var minX = Math.Min(Start.X, End.X);
            var minY = Math.Min(Start.Y, End.Y);
            var maxX = Math.Max(Start.X, End.X);
            var maxY = Math.Max(Start.Y, End.Y);
            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }
    }

    /// <summary>
    /// False for shapes whose size is driven by intrinsic state (font size,
    /// rendered text length) rather than endpoint geometry — those get a
    /// move-only selection with no resize handles.
    /// </summary>
    public virtual bool SupportsResize => true;

    /// <summary>Translate the annotation by a vector (move without resize).</summary>
    public Annotation Translate(Vector offset) =>
        WithEndpoints(Start + offset, End + offset);
}
