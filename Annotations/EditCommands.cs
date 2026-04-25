// File: Annotations/EditCommands.cs
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WinShot.Annotations;

/// <summary>
/// A reversible mutation of the annotation list. Owned by the editor's undo
/// stack. <see cref="Apply"/> mutates the list forward; <see cref="Revert"/>
/// restores the pre-edit state. All list mutations post-creation go through
/// commands — direct mutation of annotations after they're in the list is
/// not allowed (annotations are <c>init</c>-only).
/// </summary>
public interface IEditCommand
{
    void Apply(ObservableCollection<Annotation> list);
    void Revert(ObservableCollection<Annotation> list);
}

/// <summary>Appends a new annotation at the end of the list.</summary>
public sealed class AddCommand : IEditCommand
{
    private readonly Annotation _annotation;

    public AddCommand(Annotation annotation) => _annotation = annotation;

    /// <summary>Exposed so the editor's selection tracking can follow the added object.</summary>
    public Annotation Annotation => _annotation;

    public void Apply(ObservableCollection<Annotation> list) => list.Add(_annotation);
    public void Revert(ObservableCollection<Annotation> list) => list.Remove(_annotation);
}

/// <summary>
/// Swaps the annotation at <see cref="Index"/> with a new instance. Used for
/// every move and resize — the list sees a clean before-&gt;after transition
/// with no intermediate states.
/// </summary>
public sealed class ReplaceCommand : IEditCommand
{
    public int Index { get; }
    public Annotation Before { get; }
    public Annotation After { get; }

    public ReplaceCommand(int index, Annotation before, Annotation after)
    {
        Index = index;
        Before = before;
        After = after;
    }

    public void Apply(ObservableCollection<Annotation> list)  => list[Index] = After;
    public void Revert(ObservableCollection<Annotation> list) => list[Index] = Before;
}

/// <summary>Removes the annotation at <see cref="Index"/>. Undo re-inserts at the same index.</summary>
public sealed class RemoveCommand : IEditCommand
{
    public int Index { get; }
    public Annotation Annotation { get; }

    public RemoveCommand(int index, Annotation annotation)
    {
        Index = index;
        Annotation = annotation;
    }

    public void Apply(ObservableCollection<Annotation> list)  => list.RemoveAt(Index);
    public void Revert(ObservableCollection<Annotation> list) => list.Insert(Index, Annotation);
}

/// <summary>
/// Removes every annotation. Undo restores the full list in its original
/// order. Snapshotting is done at construction time so later changes to the
/// source list can't corrupt the revert state.
/// </summary>
public sealed class ClearCommand : IEditCommand
{
    private readonly Annotation[] _snapshot;

    public ClearCommand(IEnumerable<Annotation> current)
    {
        _snapshot = System.Linq.Enumerable.ToArray(current);
    }

    public void Apply(ObservableCollection<Annotation> list) => list.Clear();

    public void Revert(ObservableCollection<Annotation> list)
    {
        // Assumes Apply was run (list empty); we still guard to stay robust.
        list.Clear();
        foreach (var a in _snapshot) list.Add(a);
    }
}
