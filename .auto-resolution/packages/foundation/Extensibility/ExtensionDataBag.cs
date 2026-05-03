using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Sunfish.Foundation.Extensibility;

/// <summary>
/// Per-entity extension bag: a dictionary of <see cref="ExtensionFieldKey"/> to boxed values.
/// Not thread-safe; callers coordinate concurrent mutation.
/// </summary>
public sealed class ExtensionDataBag : IReadOnlyDictionary<ExtensionFieldKey, object?>
{
    private readonly Dictionary<ExtensionFieldKey, object?> _values = new();

    /// <summary>Count of fields currently set on this bag.</summary>
    public int Count => _values.Count;

    /// <summary>The set of keys currently present.</summary>
    public IEnumerable<ExtensionFieldKey> Keys => _values.Keys;

    /// <summary>The set of values currently present.</summary>
    public IEnumerable<object?> Values => _values.Values;

    /// <summary>Gets the raw boxed value for a key, or throws if absent.</summary>
    public object? this[ExtensionFieldKey key] => _values[key];

    /// <summary>Returns true if the bag contains a value for the given key.</summary>
    public bool ContainsKey(ExtensionFieldKey key) => _values.ContainsKey(key);

    /// <summary>Non-generic try-get used by the dictionary interface.</summary>
    public bool TryGetValue(ExtensionFieldKey key, out object? value) => _values.TryGetValue(key, out value);

    /// <summary>Returns the typed value for a key, or the type default when absent.</summary>
    public T? Get<T>(ExtensionFieldKey key)
    {
        if (!_values.TryGetValue(key, out var boxed) || boxed is null)
        {
            return default;
        }

        return boxed is T typed
            ? typed
            : throw new InvalidCastException(
                $"Extension field '{key}' holds a '{boxed.GetType().Name}', not '{typeof(T).Name}'.");
    }

    /// <summary>Attempts to read a typed value without throwing on type mismatch.</summary>
    public bool TryGet<T>(ExtensionFieldKey key, [NotNullWhen(true)] out T? value)
    {
        if (_values.TryGetValue(key, out var boxed) && boxed is T typed)
        {
            value = typed;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>Sets or replaces the value for a key. Passing null removes the entry.</summary>
    public void Set<T>(ExtensionFieldKey key, T? value)
    {
        if (value is null)
        {
            _values.Remove(key);
            return;
        }

        _values[key] = value;
    }

    /// <summary>Removes the entry for a key. Returns true if a value was removed.</summary>
    public bool Remove(ExtensionFieldKey key) => _values.Remove(key);

    /// <summary>Removes every entry.</summary>
    public void Clear() => _values.Clear();

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<ExtensionFieldKey, object?>> GetEnumerator() => _values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
