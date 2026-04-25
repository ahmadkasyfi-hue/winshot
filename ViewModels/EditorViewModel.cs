// File: ViewModels/EditorViewModel.cs
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using WinShot.Annotations;

namespace WinShot.ViewModels;

/// <summary>
/// Backs <c>EditorWindow</c>. Holds the list of annotations (oldest first,
/// rendered bottom-to-top), the currently selected tool, and current
/// color/thickness defaults.
///
/// <para>All list mutations after creation go through the command stack
/// (<see cref="Execute"/>) so that undo can reverse them. The draft
/// annotation created during a mouse-drag is NOT in the list yet, and is
/// not subject to this rule.</para>
/// </summary>
public sealed partial class EditorViewModel : ObservableObject
{
    [ObservableProperty] private AnnotationTool _currentTool = AnnotationTool.Arrow;
    [ObservableProperty] private Color _currentColor = Colors.Red;
    [ObservableProperty] private double _currentThickness = 3.0;
    [ObservableProperty] private double _currentFontSize = 20;

    /// <summary>
    /// Currently selected annotation, or null. Selection is a pure view-state
    /// concept — it isn't rendered by <c>Annotation.Render</c> (that's the
    /// single export-shared path). Selection chrome is drawn by the editor
    /// as an overlay pass.
    /// </summary>
    [ObservableProperty] private Annotation? _selected;

    public ObservableCollection<Annotation> Annotations { get; } = new();

    // LIFO undo stack. We don't have redo yet; if we add it, push undone
    // commands onto a second stack here.
    private readonly Stack<IEditCommand> _undo = new();

    /// <summary>
    /// Apply a command and record it for undo. This is the only correct path
    /// for mutating the annotations list post-creation.
    /// </summary>
    public void Execute(IEditCommand cmd)
    {
        cmd.Apply(Annotations);
        _undo.Push(cmd);
    }

    public void Add(Annotation annotation)
    {
        Execute(new AddCommand(annotation));
        // Leave selection alone — adding a fresh draft via the drawing tools
        // shouldn't yank the selection away from whatever was selected.
    }

    /// <summary>
    /// Replace the selected annotation with a new version (e.g. after a
    /// move/resize gesture) and keep selection pointed at the new instance.
    /// </summary>
    public void ReplaceSelected(Annotation newVersion)
    {
        if (Selected is null) return;
        int idx = Annotations.IndexOf(Selected);
        if (idx < 0) return;
        Execute(new ReplaceCommand(idx, Selected, newVersion));
        Selected = newVersion;
    }

    public void DeleteSelected()
    {
        if (Selected is null) return;
        int idx = Annotations.IndexOf(Selected);
        if (idx < 0) return;
        Execute(new RemoveCommand(idx, Selected));
        Selected = null;
    }

    public void Undo()
    {
        if (_undo.Count == 0) return;
        var cmd = _undo.Pop();
        cmd.Revert(Annotations);

        // If the selected annotation no longer exists (undo deleted it, or
        // undid its replacement), drop the selection. Objects are reference-
        // compared because replacements produce a fresh instance.
        if (Selected is not null && !Annotations.Contains(Selected))
            Selected = null;
    }

    public void Clear()
    {
        if (Annotations.Count == 0) return;
        Execute(new ClearCommand(Annotations.ToList()));
        Selected = null;
    }

    /// <summary>
    /// When the current tool flips away from Select, drop the selection —
    /// otherwise the user sees lingering handles while drawing a new shape.
    /// </summary>
    partial void OnCurrentToolChanged(AnnotationTool value)
    {
        if (value != AnnotationTool.Select && Selected is not null)
            Selected = null;
    }
}
