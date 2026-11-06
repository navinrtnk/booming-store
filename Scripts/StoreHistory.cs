// Copyright (c) 2026 Navin Ratnayake (@navinrtnk). All rights reserved.
namespace BoomingStore;

/// <summary>Bounded snapshots: a new edit invalidates redo; oldest entries are dropped in O(1).</summary>
public sealed class StoreHistory(int capacity = 80)
{
    private readonly LinkedList<string> _undo = new();
    private readonly LinkedList<string> _redo = new();
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public void Record(StoreState state)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        _undo.AddLast(state.Serialize(false));
        if (_undo.Count > capacity) _undo.RemoveFirst();
        _redo.Clear();
    }
    public StoreState Undo(StoreState state) => Restore(_undo, _redo, state);
    public StoreState Redo(StoreState state) => Restore(_redo, _undo, state);
    private static StoreState Restore(LinkedList<string> from, LinkedList<string> to, StoreState current)
    {
        if (from.Last == null) return current;
        var restored = StoreState.Parse(from.Last.Value);
        to.AddLast(current.Serialize(false));
        from.RemoveLast();
        return restored;
    }
    public void Clear() { _undo.Clear(); _redo.Clear(); }
}
