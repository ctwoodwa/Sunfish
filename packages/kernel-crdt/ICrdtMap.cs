namespace Sunfish.Kernel.Crdt;

/// <summary>
/// Key-value CRDT container. Concurrent writes to the same key resolve by the backend's
/// conflict rule (typically last-writer-wins keyed on a Lamport timestamp).
/// </summary>
public interface ICrdtMap
{
    /// <summary>Number of keys currently present (not tombstoned).</summary>
    int Count { get; }

    /// <summary>Enumerate keys currently present. Enumeration is a stable snapshot at call time.</summary>
    IEnumerable<string> Keys { get; }

    /// <summary>
    /// Get the value stored at <paramref name="key"/>, coerced to <typeparamref name="T"/>.
    /// Returns the default of <typeparamref name="T"/> if the key is absent or the stored
    /// value is not assignable to <typeparamref name="T"/>.
    /// </summary>
    T? Get<T>(string key);

    /// <summary>Set the value at <paramref name="key"/>. Overwrites any prior value.</summary>
    void Set<T>(string key, T value);

    /// <summary>Remove <paramref name="key"/>. Returns true if the key was present.</summary>
    bool Remove(string key);

    /// <summary>True if <paramref name="key"/> is currently present.</summary>
    bool ContainsKey(string key);

    /// <summary>Raised after a local or remote change mutates the map.</summary>
    event EventHandler<CrdtMapChangedEventArgs>? Changed;
}

/// <summary>
/// Map-container change notification.
/// </summary>
/// <param name="Key">Key affected by the change.</param>
/// <param name="IsDeleted">True if the key was removed; false if it was set.</param>
public sealed record CrdtMapChangedEventArgs(string Key, bool IsDeleted);
