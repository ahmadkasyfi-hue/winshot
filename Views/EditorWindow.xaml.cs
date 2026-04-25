// File: Views/EditorWindow.xaml.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using WinShot.Annotations;
using WinShot.Services;
using WinShot.ViewModels;

namespace WinShot.Views;

/// <summary>
/// Post-capture annotation editor.
///
/// Layering (Grid children stack top-down):
///   1. Image (the screenshot)  — bottom
///   2. ContentControl → VisualHost → DrawingVisual for non-destructive annotations
///   3. InteractionCanvas (transparent, catches mouse)
///
/// At export time we replay all annotations into a RenderTargetBitmap so the
/// saved PNG is pixel-identical to what the user sees. Selection chrome is
/// editor-only and never reaches the export path.
/// </summary>
public partial class EditorWindow : Window
{
    private readonly BitmapSource _screenshot;
    private readonly IClipboardService _clipboard;
    private readonly ISettingsService _settings;
    private readonly ILogger<EditorWindow> _logger;
    private readonly EditorViewModel _vm = new();
    private readonly VisualHost _visualHost = new();

    // In-flight draft while creating a new annotation (non-Select tools).
    private Annotation? _draft;
    private Point _dragStart;
    private bool _dragging;

    // In-flight edit gesture (Select tool). The ghost is the transformed
    // version shown during the drag; we never mutate the list mid-gesture.
    // On MouseUp we push ONE ReplaceCommand going from _editOriginal -> _editGhost.
    private Annotation? _editOriginal;
    private Annotation? _editGhost;
    private Handle _editHandle = Handle.None;
    private Point _editDragStart;

    // Stock palette, ordered so hot colors come first for quick access.
    private static readonly Color[] Palette =
    {
        Colors.Red, Colors.OrangeRed, Colors.Gold, Colors.LimeGreen,
        Colors.DeepSkyBlue, Colors.Violet, Colors.White, Colors.Black,
    };

    // Single-letter accelerators. Suppressed while the inline TextBox has focus.
    private static readonly (Key Key, AnnotationTool Tool)[] ToolShortcuts =
    {
        (Key.S, AnnotationTool.Select),
        (Key.A, AnnotationTool.Arrow),
        (Key.R, AnnotationTool.Rectangle),
        (Key.E, AnnotationTool.Ellipse),
        (Key.L, AnnotationTool.Line),
        (Key.T, AnnotationTool.Text),
        (Key.H, AnnotationTool.Highlighter),
        (Key.B, AnnotationTool.Blur),
    };

    /// <summary>Half-side length of a selection handle in image pixels.</summary>
    private const double HandleHalf = 5.0;

    /// <summary>
    /// Handles for the selected annotation. StartPoint/EndPoint are used by
    /// 1-D shapes (line/arrow/highlighter); the eight bbox positions are used
    /// by 2-D shapes (rect/ellipse/blur). Body = dragging inside for a move.
    /// </summary>
    private enum Handle
    {
        None, Body,
        StartPoint, EndPoint,
        TopLeft, Top, TopRight,
        Left, Right,
        BottomLeft, Bottom, BottomRight,
    }

    public EditorWindow(
        BitmapSource screenshot,
        IClipboardService clipboard,
        ISettingsService settings,
        ILogger<EditorWindow> logger)
    {
        InitializeComponent();

        _screenshot = screenshot ?? throw new ArgumentNullException(nameof(screenshot));
        _clipboard  = clipboard  ?? throw new ArgumentNullException(nameof(clipboard));
        _settings   = settings   ?? throw new ArgumentNullException(nameof(settings));
        _logger     = logger     ?? throw new ArgumentNullException(nameof(logger));

        DataContext = _vm;

        // CreateBitmapSourceFromHBitmap returns a BitmapSource with DpiX=DpiY=96,
        // so PixelWidth == DIP width. Setting the host size to the pixel size
        // gives 1:1 on-screen correspondence to the captured pixels.
        Screenshot.Source = _screenshot;
        CanvasHost.Width = _screenshot.PixelWidth;
        CanvasHost.Height = _screenshot.PixelHeight;
        InteractionCanvas.Width = _screenshot.PixelWidth;
        InteractionCanvas.Height = _screenshot.PixelHeight;
        InkHost.Content = _visualHost;

        // Color swatches.
        foreach (var color in Palette)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            var swatch = new Button
            {
                Background = brush,
                Width = 22, Height = 22,
                Margin = new Thickness(2, 0, 2, 0),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x50, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                Tag = color,
                Focusable = false,
            };
            swatch.Click += (_, _) => _vm.CurrentColor = (Color)swatch.Tag;
            ColorStrip.Items.Add(swatch);
        }

        // Any list change OR a selection change triggers a full repaint —
        // selection chrome is drawn by Redraw, not by a separate overlay
        // visual, so we need to redraw on both.
        _vm.Annotations.CollectionChanged += (_, _) => Redraw();
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(EditorViewModel.Selected)) Redraw();
        };

        // Default tool.
        ToolArrow.IsChecked = true;
        _vm.CurrentTool = AnnotationTool.Arrow;

        UpdateSaveDirHint();
        _settings.SettingsChanged += (_, _) => UpdateSaveDirHint();

        // Ctrl-shortcuts via InputBindings (work regardless of focus).
        InputBindings.Add(new KeyBinding(new RelayCommand(_vm.Undo),
            new KeyGesture(Key.Z, ModifierKeys.Control)));
        InputBindings.Add(new KeyBinding(new RelayCommand(() => Execute(ExportAction.Copy)),
            new KeyGesture(Key.C, ModifierKeys.Control)));
        InputBindings.Add(new KeyBinding(new RelayCommand(() => Execute(ExportAction.Save)),
            new KeyGesture(Key.S, ModifierKeys.Control)));
        InputBindings.Add(new KeyBinding(new RelayCommand(() => Execute(ExportAction.SaveAndCopy)),
            new KeyGesture(Key.Enter, ModifierKeys.Control)));
        InputBindings.Add(new KeyBinding(new RelayCommand(Close),
            new KeyGesture(Key.Escape, ModifierKeys.None)));

        // Single-letter tool shortcuts via PreviewKeyDown so we can suppress
        // while the TextBox has focus. Using Preview is important because
        // bubbling KeyDown from the TextBox is marked handled.
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void UpdateSaveDirHint()
    {
        var s = _settings.Current;
        SaveDirHint.Text = $"Saves to: {s.SaveDirectory}  ({s.DefaultFormat.ToUpperInvariant()})";
    }

    // ----- Toolbar handlers -----

    private void OnToolClicked(object sender, RoutedEventArgs e)
    {
        var clicked = (ToggleButton)sender;
        SelectTool(Enum.Parse<AnnotationTool>((string)clicked.Tag));
    }

    /// <summary>Sets the current tool and synchronizes the toggle-button row.</summary>
    private void SelectTool(AnnotationTool tool)
    {
        _vm.CurrentTool = tool;

        ToolSelect.IsChecked      = tool == AnnotationTool.Select;
        ToolArrow.IsChecked       = tool == AnnotationTool.Arrow;
        ToolRect.IsChecked        = tool == AnnotationTool.Rectangle;
        ToolEllipse.IsChecked     = tool == AnnotationTool.Ellipse;
        ToolLine.IsChecked        = tool == AnnotationTool.Line;
        ToolText.IsChecked        = tool == AnnotationTool.Text;
        ToolHighlighter.IsChecked = tool == AnnotationTool.Highlighter;
        ToolBlur.IsChecked        = tool == AnnotationTool.Blur;

        _logger.LogDebug("Tool changed to {Tool}", tool);
    }

    private void OnThicknessChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _vm.CurrentThickness = e.NewValue;
        if (ThicknessText is not null) ThicknessText.Text = ((int)e.NewValue).ToString();
    }

    private void OnUndo(object sender, RoutedEventArgs e) => _vm.Undo();
    private void OnClear(object sender, RoutedEventArgs e) => _vm.Clear();

    private void OnActionCopy(object sender, RoutedEventArgs e)        => Execute(ExportAction.Copy);
    private void OnActionSave(object sender, RoutedEventArgs e)        => Execute(ExportAction.Save);
    private void OnActionSaveAndCopy(object sender, RoutedEventArgs e) => Execute(ExportAction.SaveAndCopy);
    private void OnSaveAs(object sender, RoutedEventArgs e)            => SaveAs();

    private void OnOpenSettings(object sender, RoutedEventArgs e)
    {
        var dlg = new SettingsWindow(_settings) { Owner = this };
        dlg.ShowDialog();
    }

    private void OnExitApp(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(this,
            "Exit WinShot? The global hotkey (Ctrl+Shift+S) will stop working until you launch it again.",
            "WinShot",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);
        if (result == MessageBoxResult.OK)
            Application.Current.Shutdown();
    }

    // ----- Keyboard: single-letter tool shortcuts + Delete -----

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Don't fire while typing a label, editing the filename TextBox, or
        // while any modifier keys are held (those are reserved for Ctrl+X actions).
        if (Keyboard.FocusedElement is TextBox) return;
        if (Keyboard.Modifiers != ModifierKeys.None) return;

        if (e.Key == Key.Delete && _vm.Selected is not null)
        {
            _vm.DeleteSelected();
            e.Handled = true;
            return;
        }

        foreach (var (key, tool) in ToolShortcuts)
        {
            if (e.Key == key)
            {
                SelectTool(tool);
                e.Handled = true;
                return;
            }
        }
    }

    // ----- Canvas interaction -----

    private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
    {
        // Commit any pending text entry before processing a new click —
        // Canvas isn't focusable, so TextBox.LostFocus wouldn't fire on its own.
        if (_pendingText is not null) CommitText();

        var p = e.GetPosition(InteractionCanvas);

        if (_vm.CurrentTool == AnnotationTool.Select)
        {
            StartSelectGesture(p);
            return;
        }

        if (_vm.CurrentTool == AnnotationTool.Text)
        {
            StartTextEntry(p);
            return;
        }

        _dragStart = p;
        _dragging = true;
        _draft = CreateDraft(p, p);
        InteractionCanvas.CaptureMouse();
    }

    private void OnCanvasMouseMove(object sender, MouseEventArgs e)
    {
        var p = e.GetPosition(InteractionCanvas);

        // Cursor feedback when hovering in Select tool, no drag in progress.
        if (_vm.CurrentTool == AnnotationTool.Select && _editOriginal is null && !_dragging)
        {
            UpdateHoverCursor(p);
        }

        if (_editOriginal is not null)
        {
            var delta = p - _editDragStart;
            _editGhost = ApplyHandleDrag(_editOriginal, _editHandle, delta);
            Redraw();
            return;
        }

        if (!_dragging || _draft is null) return;

        // Draft-gesture update: rebuild the draft with the new End (annotations
        // are init-only, so we swap the whole instance).
        _draft = _draft.WithEndpoints(_draft.Start, p);
        RedrawWithDraft();
    }

    private void OnCanvasMouseUp(object sender, MouseButtonEventArgs e)
    {
        var p = e.GetPosition(InteractionCanvas);

        // Complete an edit gesture (Select-tool move/resize)?
        if (_editOriginal is not null)
        {
            InteractionCanvas.ReleaseMouseCapture();

            var delta = p - _editDragStart;
            var finalGhost = ApplyHandleDrag(_editOriginal, _editHandle, delta);
            var original = _editOriginal;

            // Tear down edit state BEFORE mutating the list. Execute() below
            // raises CollectionChanged synchronously → Redraw runs during the
            // call; it must see clean (_editOriginal == null) state so it
            // doesn't try to render a stale ghost.
            _editOriginal = null;
            _editGhost = null;
            _editHandle = Handle.None;

            // Skip no-op drags (click without movement): saves an undo entry
            // that would do nothing.
            if (finalGhost.Start != original.Start || finalGhost.End != original.End)
            {
                int idx = _vm.Annotations.IndexOf(original);
                if (idx >= 0)
                {
                    _vm.Execute(new ReplaceCommand(idx, original, finalGhost));
                    _vm.Selected = finalGhost;
                }
            }

            Redraw();
            return;
        }

        // Complete a draft draw?
        if (!_dragging || _draft is null) return;
        _dragging = false;
        InteractionCanvas.ReleaseMouseCapture();

        var committed = _draft.WithEndpoints(_draft.Start, p);
        if ((p - _dragStart).Length >= 3)
            _vm.Add(committed);

        _draft = null;
        Redraw();
    }

    /// <summary>Handle selection/handle/body/empty dispatch on a Select-tool click.</summary>
    private void StartSelectGesture(Point p)
    {
        // 1. Clicked on a handle of the currently selected annotation?
        if (_vm.Selected is not null)
        {
            var h = HitTestHandle(_vm.Selected, p);
            if (h != Handle.None)
            {
                BeginEdit(_vm.Selected, h, p);
                return;
            }
        }

        // 2. Hit-test all annotations (topmost first — list is bottom-up).
        for (int i = _vm.Annotations.Count - 1; i >= 0; i--)
        {
            var a = _vm.Annotations[i];
            if (a.HitTest(p))
            {
                _vm.Selected = a;
                BeginEdit(a, Handle.Body, p);
                return;
            }
        }

        // 3. Click on empty space — clear selection.
        _vm.Selected = null;
    }

    private void BeginEdit(Annotation target, Handle handle, Point anchor)
    {
        _editOriginal = target;
        _editGhost = target;
        _editHandle = handle;
        _editDragStart = anchor;
        InteractionCanvas.CaptureMouse();
    }

    private Annotation CreateDraft(Point start, Point end)
    {
        var color = _vm.CurrentColor;
        var th = _vm.CurrentThickness;
        return _vm.CurrentTool switch
        {
            AnnotationTool.Arrow       => new ArrowAnnotation       { Start = start, End = end, Color = color, Thickness = th },
            AnnotationTool.Rectangle   => new RectangleAnnotation   { Start = start, End = end, Color = color, Thickness = th },
            AnnotationTool.Ellipse     => new EllipseAnnotation     { Start = start, End = end, Color = color, Thickness = th },
            AnnotationTool.Line        => new LineAnnotation        { Start = start, End = end, Color = color, Thickness = th },
            AnnotationTool.Highlighter => new HighlighterAnnotation { Start = start, End = end, Color = color, Thickness = Math.Max(10, th * 3) },
            AnnotationTool.Blur        => new BlurAnnotation        { Start = start, End = end, Color = color, Thickness = th, SourceImage = _screenshot },
            _                          => new ArrowAnnotation       { Start = start, End = end, Color = color, Thickness = th },
        };
    }

    // ----- Text entry -----

    private TextAnnotation? _pendingText;
    private Point _pendingTextAnchor;

    private void StartTextEntry(Point p)
    {
        InlineTextBox.Text = string.Empty;
        InlineTextBox.FontSize = _vm.CurrentFontSize;
        InlineTextBox.Foreground = new SolidColorBrush(_vm.CurrentColor);
        Canvas.SetLeft(InlineTextBox, p.X);
        Canvas.SetTop(InlineTextBox, p.Y);
        InlineTextBox.Visibility = Visibility.Visible;
        InlineTextBox.Focus();

        _pendingTextAnchor = p;
        _pendingText = new TextAnnotation
        {
            Start = p,
            End = p,
            Color = _vm.CurrentColor,
            FontSize = _vm.CurrentFontSize,
        };
    }

    private void OnInlineTextBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)  { CommitText(); e.Handled = true; }
        else if (e.Key == Key.Escape) { CancelText(); e.Handled = true; }
    }

    private void OnInlineTextBoxLostFocus(object sender, RoutedEventArgs e) => CommitText();

    private void CommitText()
    {
        if (_pendingText is null) return;
        var text = InlineTextBox.Text?.Trim() ?? string.Empty;
        if (text.Length > 0)
        {
            // Nudge the anchor slightly so the painted glyphs don't overlap
            // the TextBox border the user was just typing into. Build a fresh
            // instance — annotations are init-only.
            var anchor = new Point(_pendingTextAnchor.X + 3, _pendingTextAnchor.Y + 1);
            var committed = new TextAnnotation
            {
                Start = anchor,
                End = anchor,
                Color = _pendingText.Color,
                Thickness = _pendingText.Thickness,
                Text = text,
                FontSize = _pendingText.FontSize,
                FontFamily = _pendingText.FontFamily,
                Bold = _pendingText.Bold,
            };
            _vm.Add(committed);
        }
        CancelText();
    }

    private void CancelText()
    {
        InlineTextBox.Visibility = Visibility.Collapsed;
        _pendingText = null;
        Keyboard.ClearFocus();
    }

    // ----- Selection handles / hit-test helpers -----

    private static IEnumerable<(Handle Handle, Point Pos)> HandlePositions(Annotation a)
    {
        if (a is LineAnnotation or ArrowAnnotation or HighlighterAnnotation)
        {
            yield return (Handle.StartPoint, a.Start);
            yield return (Handle.EndPoint, a.End);
            yield break;
        }

        if (!a.SupportsResize) yield break;

        var b = a.Bounds;
        yield return (Handle.TopLeft,     new Point(b.Left,                   b.Top));
        yield return (Handle.Top,         new Point(b.Left + b.Width / 2,     b.Top));
        yield return (Handle.TopRight,    new Point(b.Right,                  b.Top));
        yield return (Handle.Left,        new Point(b.Left,                   b.Top + b.Height / 2));
        yield return (Handle.Right,       new Point(b.Right,                  b.Top + b.Height / 2));
        yield return (Handle.BottomLeft,  new Point(b.Left,                   b.Bottom));
        yield return (Handle.Bottom,      new Point(b.Left + b.Width / 2,     b.Bottom));
        yield return (Handle.BottomRight, new Point(b.Right,                  b.Bottom));
    }

    private static Handle HitTestHandle(Annotation a, Point p)
    {
        // Pick tolerance 1px larger than the drawn half-size so the hit zone
        // fully contains the visible handle square.
        double slop = HandleHalf + 1;
        foreach (var (h, pos) in HandlePositions(a))
        {
            if (Math.Abs(p.X - pos.X) <= slop && Math.Abs(p.Y - pos.Y) <= slop)
                return h;
        }
        return Handle.None;
    }

    /// <summary>
    /// Compute the transformed annotation for a drag on <paramref name="handle"/>
    /// by <paramref name="delta"/>. For 1-D shapes only the grabbed endpoint
    /// (or both, for Body) moves. For 2-D shapes we work in Bounds-space so
    /// Start/End ordering (which Normalize flips anyway) doesn't matter.
    /// </summary>
    private static Annotation ApplyHandleDrag(Annotation original, Handle handle, Vector delta)
    {
        if (original is LineAnnotation or ArrowAnnotation or HighlighterAnnotation)
        {
            return handle switch
            {
                Handle.StartPoint => original.WithEndpoints(original.Start + delta, original.End),
                Handle.EndPoint   => original.WithEndpoints(original.Start, original.End + delta),
                Handle.Body       => original.Translate(delta),
                _                 => original,
            };
        }

        // Bbox-style shapes. For Text (SupportsResize == false) only Body
        // reaches here; skip the resize math.
        if (!original.SupportsResize || handle == Handle.Body)
            return original.Translate(delta);

        var b = original.Bounds;
        double l = b.Left, t = b.Top, r = b.Right, btm = b.Bottom;

        switch (handle)
        {
            case Handle.TopLeft:     l += delta.X; t   += delta.Y; break;
            case Handle.TopRight:    r += delta.X; t   += delta.Y; break;
            case Handle.BottomLeft:  l += delta.X; btm += delta.Y; break;
            case Handle.BottomRight: r += delta.X; btm += delta.Y; break;
            case Handle.Top:                       t   += delta.Y; break;
            case Handle.Bottom:                    btm += delta.Y; break;
            case Handle.Left:        l += delta.X;                 break;
            case Handle.Right:       r += delta.X;                 break;
            default:                                               break;
        }

        // Allow inversion during drag (e.g. pulling TopLeft past BottomRight)
        // — RectangleAnnotation.Normalize already handles flipped corners at
        // render time, and users expect the shape to follow the cursor.
        return original.WithEndpoints(new Point(l, t), new Point(r, btm));
    }

    private void UpdateHoverCursor(Point p)
    {
        // Handle hit first — corner/edge cursors are more informative.
        if (_vm.Selected is not null)
        {
            var h = HitTestHandle(_vm.Selected, p);
            var cur = CursorForHandle(h);
            if (cur is not null) { Cursor = cur; return; }
        }

        // Body hit anywhere?
        for (int i = _vm.Annotations.Count - 1; i >= 0; i--)
        {
            if (_vm.Annotations[i].HitTest(p)) { Cursor = Cursors.SizeAll; return; }
        }

        Cursor = Cursors.Arrow;
    }

    private static Cursor? CursorForHandle(Handle h) => h switch
    {
        Handle.TopLeft or Handle.BottomRight => Cursors.SizeNWSE,
        Handle.TopRight or Handle.BottomLeft => Cursors.SizeNESW,
        Handle.Top or Handle.Bottom          => Cursors.SizeNS,
        Handle.Left or Handle.Right          => Cursors.SizeWE,
        Handle.StartPoint or Handle.EndPoint => Cursors.Cross,
        Handle.Body                          => Cursors.SizeAll,
        _                                    => null,
    };

    // ----- Rendering -----

    /// <summary>
    /// Repaint the editor overlay. For each annotation, if it's the one being
    /// edited right now, draw the ghost (the transformed in-flight version)
    /// instead. Finally, draw selection chrome as an editor-only overlay.
    /// Selection chrome is NOT rendered by <c>RenderFlattened</c> — Invariant #3.
    /// </summary>
    private void Redraw()
    {
        var size = new Size(_screenshot.PixelWidth, _screenshot.PixelHeight);
        using var dc = _visualHost.Visual.RenderOpen();
        foreach (var a in _vm.Annotations)
        {
            if (ReferenceEquals(a, _editOriginal) && _editGhost is not null)
                _editGhost.Render(dc, size);
            else
                a.Render(dc, size);
        }
        DrawSelectionChrome(dc);
    }

    private void RedrawWithDraft()
    {
        var size = new Size(_screenshot.PixelWidth, _screenshot.PixelHeight);
        using var dc = _visualHost.Visual.RenderOpen();
        foreach (var a in _vm.Annotations)
            a.Render(dc, size);
        _draft?.Render(dc, size);
        DrawSelectionChrome(dc);
    }

    /// <summary>
    /// Dashed outline + handles for the selected annotation. When a drag is
    /// in flight we draw chrome around the ghost, not the original, so the
    /// handles track the cursor.
    /// </summary>
    private void DrawSelectionChrome(DrawingContext dc)
    {
        var focus = _editGhost ?? _vm.Selected;
        if (focus is null) return;

        var bluePen = new Pen(new SolidColorBrush(Color.FromRgb(0x4F, 0x8E, 0xF7)), 1.0)
        {
            DashStyle = new DashStyle(new double[] { 4, 3 }, 0),
        };
        bluePen.Freeze();

        // Dashed bounding rect.
        var b = focus.Bounds;
        if (b.Width > 0 && b.Height > 0)
            dc.DrawRectangle(brush: null, bluePen, Rect.Inflate(b, 2, 2));

        // Handle squares — solid white fill with a blue border.
        var fill = new SolidColorBrush(Colors.White);
        fill.Freeze();
        var border = new Pen(new SolidColorBrush(Color.FromRgb(0x4F, 0x8E, 0xF7)), 1.0);
        border.Freeze();

        foreach (var (_, pos) in HandlePositions(focus))
        {
            var handleRect = new Rect(pos.X - HandleHalf, pos.Y - HandleHalf, HandleHalf * 2, HandleHalf * 2);
            dc.DrawRectangle(fill, border, handleRect);
        }
    }

    // ----- Export -----

    private enum ExportAction { Copy, Save, SaveAndCopy }

    /// <summary>
    /// Flatten annotations onto the screenshot. This is the shared render path
    /// with the editor preview (Invariant #3) — except selection chrome,
    /// which is deliberately NOT drawn here so exports stay clean.
    /// </summary>
    private BitmapSource RenderFlattened()
    {
        var size = new Size(_screenshot.PixelWidth, _screenshot.PixelHeight);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawImage(_screenshot, new Rect(0, 0, size.Width, size.Height));
            foreach (var a in _vm.Annotations)
                a.Render(dc, size);
        }

        var rtb = new RenderTargetBitmap(
            _screenshot.PixelWidth, _screenshot.PixelHeight,
            _screenshot.DpiX, _screenshot.DpiY,
            PixelFormats.Pbgra32);
        rtb.Render(visual);
        rtb.Freeze();
        return rtb;
    }

    private void Execute(ExportAction action)
    {
        try
        {
            var bmp = RenderFlattened();
            string? savedPath = null;

            if (action == ExportAction.Copy || action == ExportAction.SaveAndCopy)
                _clipboard.SetImage(bmp);

            if (action == ExportAction.Save || action == ExportAction.SaveAndCopy)
                savedPath = SaveToConfiguredPath(bmp);

            _logger.LogInformation("Export finished: action={Action}, saved={Saved}", action, savedPath ?? "(none)");

            if (savedPath is not null && _settings.Current.RevealInExplorerAfterSave)
                RevealInExplorer(savedPath);

            // For save-involved actions we close the editor — that matches the
            // common Snipping-Tool-style "done" flow. Copy alone keeps the
            // editor open so the user can iterate.
            if (action != ExportAction.Copy)
                Close();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export failed ({Action})", action);
            MessageBox.Show(this, ex.Message, "WinShot — Export failed",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private string SaveToConfiguredPath(BitmapSource bmp)
    {
        var path = _settings.Current.ResolveFilePath(DateTime.Now);

        BitmapEncoder encoder = _settings.Current.DefaultFormat.Equals("jpg", StringComparison.OrdinalIgnoreCase)
            ? new JpegBitmapEncoder { QualityLevel = Math.Clamp(_settings.Current.JpegQuality, 1, 100) }
            : new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bmp));

        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        encoder.Save(fs);
        return path;
    }

    private void SaveAs()
    {
        var dlg = new SaveFileDialog
        {
            Filter = "PNG image|*.png|JPEG image|*.jpg;*.jpeg",
            DefaultExt = ".png",
            FileName = $"WinShot_{DateTime.Now:yyyyMMdd_HHmmss}.png",
            InitialDirectory = _settings.Current.SaveDirectory,
        };
        if (dlg.ShowDialog(this) != true) return;

        try
        {
            var bmp = RenderFlattened();
            BitmapEncoder encoder = dlg.FilterIndex == 2
                ? new JpegBitmapEncoder { QualityLevel = Math.Clamp(_settings.Current.JpegQuality, 1, 100) }
                : new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bmp));

            using var fs = new FileStream(dlg.FileName, FileMode.Create, FileAccess.Write);
            encoder.Save(fs);

            _logger.LogInformation("Saved (ad hoc) to {Path}", dlg.FileName);

            if (_settings.Current.RevealInExplorerAfterSave)
                RevealInExplorer(dlg.FileName);

            Close();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Save As failed");
            MessageBox.Show(this, ex.Message, "WinShot — Save failed",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void RevealInExplorer(string filePath)
    {
        // /select, opens Explorer to the parent folder with the file highlighted.
        // Quote the path so spaces work, and use UseShellExecute=false + explicit
        // arguments to avoid the injection risks of a raw cmd /c invocation.
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{filePath}\"",
            UseShellExecute = false,
        });
    }
}

/// <summary>
/// Host control that owns a single <see cref="DrawingVisual"/>. This is the
/// WPF-idiomatic way to render arbitrary geometry without building a big
/// visual tree of Shape objects for every annotation.
/// </summary>
internal sealed class VisualHost : FrameworkElement
{
    public DrawingVisual Visual { get; } = new();

    public VisualHost()
    {
        AddVisualChild(Visual);
        AddLogicalChild(Visual);
    }

    protected override int VisualChildrenCount => 1;
    protected override Visual GetVisualChild(int index) => Visual;
}

/// <summary>Minimal ICommand for KeyBinding wiring.</summary>
internal sealed class RelayCommand : ICommand
{
    private readonly Action _action;
    public RelayCommand(Action action) => _action = action;
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _action();
    public event EventHandler? CanExecuteChanged;
}
