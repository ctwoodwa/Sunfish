namespace Sunfish.Foundation.BusinessLogic;

/// <summary>
/// A bounded snapshot stack that provides multi-level undo/redo for any
/// <see cref="BusinessObjectBase{T}"/>. Snapshots are plain dictionaries
/// produced by <see cref="FieldManager.GetSnapshot"/>.
/// </summary>
public sealed class UndoStack
{
    private readonly int _maxDepth;
    private readonly Stack<Dictionary<string, object?>> _undoStack = new();
    private readonly Stack<Dictionary<string, object?>> _redoStack = new();

    /// <param name="maxDepth">Maximum number of undo steps retained. Default 50.</param>
    public UndoStack(int maxDepth = 50) => _maxDepth = maxDepth;

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;
    public int UndoDepth => _undoStack.Count;

    // ── Push ────────────────────────────────────────────────────────────

    /// <summary>
    /// Captures the current snapshot before a change is applied.
    /// Call this immediately before mutating the <see cref="FieldManager"/>.
    /// </summary>
    public void Push(Dictionary<string, object?> snapshot)
    {
        _undoStack.Push(snapshot);
        _redoStack.Clear(); // branching history discards redo stack
        while (_undoStack.Count > _maxDepth) PopBottom(_undoStack);
    }

    // ── Undo ────────────────────────────────────────────────────────────

    /// <summary>
    /// Pops the most recent snapshot and returns it for restoration.
    /// The caller is responsible for passing it to
    /// <see cref="FieldManager.RestoreSnapshot"/>.
    /// </summary>
    /// <param name="currentSnapshot">The snapshot of the state BEFORE undoing, for redo.</param>
    public Dictionary<string, object?> Undo(Dictionary<string, object?> currentSnapshot)
    {
        if (!CanUndo) throw new InvalidOperationException("Nothing to undo.");
        _redoStack.Push(currentSnapshot);
        return _undoStack.Pop();
    }

    // ── Redo ────────────────────────────────────────────────────────────

    public Dictionary<string, object?> Redo(Dictionary<string, object?> currentSnapshot)
    {
        if (!CanRedo) throw new InvalidOperationException("Nothing to redo.");
        _undoStack.Push(currentSnapshot);
        return _redoStack.Pop();
    }

    // ── Discard ─────────────────────────────────────────────────────────

    /// <summary>Clears both stacks (e.g., after a successful save).</summary>
    public void Discard()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static void PopBottom<T>(Stack<T> stack)
    {
        var temp = new Stack<T>(stack.Count - 1);
        while (stack.Count > 1) temp.Push(stack.Pop());
        stack.Pop(); // discard bottom
        while (temp.Count > 0) stack.Push(temp.Pop());
    }
}
