using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Sunfish.Foundation.Catalog.Bundles;

namespace Sunfish.Foundation.Integrations;

/// <summary>Default in-memory <see cref="IProviderRegistry"/>. Safe for concurrent reads after startup.</summary>
public sealed class InMemoryProviderRegistry : IProviderRegistry
{
    private readonly ConcurrentDictionary<string, ProviderDescriptor> _byKey = new(StringComparer.Ordinal);
    private readonly List<string> _registrationOrder = new();
    private readonly object _orderLock = new();

    /// <inheritdoc />
    public void Register(ProviderDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (!_byKey.TryAdd(descriptor.Key, descriptor))
        {
            throw new InvalidOperationException($"Provider '{descriptor.Key}' is already registered.");
        }

        lock (_orderLock)
        {
            _registrationOrder.Add(descriptor.Key);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ProviderDescriptor> GetAll()
    {
        lock (_orderLock)
        {
            return _registrationOrder.Select(k => _byKey[k]).ToArray();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ProviderDescriptor> GetByCategory(ProviderCategory category)
    {
        lock (_orderLock)
        {
            return _registrationOrder
                .Select(k => _byKey[k])
                .Where(d => d.Category == category)
                .ToArray();
        }
    }

    /// <inheritdoc />
    public bool TryGet(string key, [NotNullWhen(true)] out ProviderDescriptor? descriptor)
        => _byKey.TryGetValue(key, out descriptor);
}
