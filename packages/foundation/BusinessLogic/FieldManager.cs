namespace Sunfish.Foundation.BusinessLogic;

/// <summary>
/// Per-instance value store for a <see cref="BusinessObjectBase{T}"/>.
/// Tracks dirty state and supports point-in-time snapshots for undo.
/// </summary>
public sealed class FieldManager
{
    private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);
    private readonly HashSet<string> _dirtyKeys = new(StringComparer.Ordinal);

    // ── Read / Write ────────────────────────────────────────────────────

    /// <summary>Returns the stored value for <paramref name="property"/>, or its default.</summary>
    public TValue Read<TValue>(PropertyInfo<TValue> property)
    {
        return _values.TryGetValue(property.Name, out var raw) && raw is TValue typed
            ? typed
            : property.DefaultValue;
    }

    /// <summary>
    /// Writes <paramref name="value"/> for <paramref name="property"/> and marks the
    /// key dirty when the value actually changed.
    /// </summary>
    /// <returns><c>true</c> when the stored value changed.</returns>
    public bool Write<TValue>(PropertyInfo<TValue> property, TValue value)
    {
        var current = Read(property);
        if (EqualityComparer<TValue>.Default.Equals(current, value)) return false;

        _values[property.Name] = value;
        _dirtyKeys.Add(property.Name);
        return true;
    }

    // ── Dirty tracking ──────────────────────────────────────────────────

    /// <summary>True when any property has an uncommitted change.</summary>
    public bool IsDirty => _dirtyKeys.Count > 0;

    /// <summary>True when the specific property has an uncommitted change.</summary>
    public bool IsPropertyDirty<TValue>(PropertyInfo<TValue> property)
        => _dirtyKeys.Contains(property.Name);

    /// <summary>Clears all dirty flags (call after a successful save).</summary>
    public void MarkClean() => _dirtyKeys.Clear();

    /// <summary>Clears the dirty flag for one property.</summary>
    public void MarkClean<TValue>(PropertyInfo<TValue> property)
        => _dirtyKeys.Remove(property.Name);

    // ── Snapshot / restore ──────────────────────────────────────────────

    /// <summary>Returns a shallow clone of the current value dictionary.</summary>
    public Dictionary<string, object?> GetSnapshot()
        => new(_values, StringComparer.Ordinal);

    /// <summary>Replaces the value dictionary with a previously captured snapshot.</summary>
    public void RestoreSnapshot(Dictionary<string, object?> snapshot)
    {
        _values.Clear();
        foreach (var kv in snapshot) _values[kv.Key] = kv.Value;
        _dirtyKeys.Clear();
    }
}
