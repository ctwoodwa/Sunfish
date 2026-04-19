using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Sunfish.Foundation.Extensibility;

namespace Sunfish.Foundation.Catalog.ExtensionFields;

/// <summary>
/// Default in-memory implementation of <see cref="IExtensionFieldCatalog"/>.
/// Safe for concurrent reads after startup registration.
/// </summary>
public sealed class ExtensionFieldCatalog : IExtensionFieldCatalog
{
    private readonly ConcurrentDictionary<Type, List<ExtensionFieldSpec>> _byEntity = new();

    /// <inheritdoc />
    public void Register(Type entityType, ExtensionFieldSpec spec)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        ArgumentNullException.ThrowIfNull(spec);

        var list = _byEntity.GetOrAdd(entityType, _ => new List<ExtensionFieldSpec>());
        lock (list)
        {
            if (list.Any(existing => existing.Key.Equals(spec.Key)))
            {
                throw new InvalidOperationException(
                    $"Extension field '{spec.Key}' is already registered on '{entityType.Name}'.");
            }

            list.Add(spec);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ExtensionFieldSpec> GetFields(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        if (!_byEntity.TryGetValue(entityType, out var list))
        {
            return Array.Empty<ExtensionFieldSpec>();
        }

        lock (list)
        {
            return list.ToArray();
        }
    }

    /// <inheritdoc />
    public bool TryGetField(Type entityType, ExtensionFieldKey key, [NotNullWhen(true)] out ExtensionFieldSpec? spec)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        if (_byEntity.TryGetValue(entityType, out var list))
        {
            lock (list)
            {
                spec = list.FirstOrDefault(s => s.Key.Equals(key));
                return spec is not null;
            }
        }

        spec = null;
        return false;
    }
}
