namespace Sunfish.Kernel.Crdt;

/// <summary>
/// Ordered-list CRDT container. Concurrent inserts at the same index resolve via the
/// backend's deterministic total order (typically fractional positions or RGA-style IDs).
/// </summary>
public interface ICrdtList
{
    /// <summary>Number of items currently present.</summary>
    int Count { get; }

    /// <summary>
    /// Get the item at <paramref name="index"/>, coerced to <typeparamref name="T"/>.
    /// Returns the default of <typeparamref name="T"/> if the index is out of range or the
    /// stored value is not assignable to <typeparamref name="T"/>.
    /// </summary>
    T? Get<T>(int index);

    /// <summary>Insert <paramref name="value"/> before the item currently at <paramref name="index"/>.</summary>
    void Insert<T>(int index, T value);

    /// <summary>Append <paramref name="value"/> at the end of the list.</summary>
    void Push<T>(T value);

    /// <summary>Remove the item at <paramref name="index"/>. Returns true if anything was removed.</summary>
    bool RemoveAt(int index);

    /// <summary>Raised after a local or remote change mutates the list.</summary>
    event EventHandler<CrdtListChangedEventArgs>? Changed;
}

/// <summary>
/// List-container change notification.
/// </summary>
/// <param name="Index">Starting index of the change.</param>
/// <param name="InsertedCount">Number of items inserted (0 if pure delete).</param>
/// <param name="DeletedCount">Number of items removed (0 if pure insert).</param>
public sealed record CrdtListChangedEventArgs(int Index, int InsertedCount, int DeletedCount);
