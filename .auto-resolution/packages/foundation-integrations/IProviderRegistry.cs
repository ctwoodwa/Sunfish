using System.Diagnostics.CodeAnalysis;
using Sunfish.Foundation.Catalog.Bundles;

namespace Sunfish.Foundation.Integrations;

/// <summary>
/// Registry of provider adapters that have been wired into the host.
/// Consumed by Bridge admin, bundle activation, and modules that need to
/// enumerate available providers per category.
/// </summary>
public interface IProviderRegistry
{
    /// <summary>Registers a provider. Duplicate keys throw.</summary>
    void Register(ProviderDescriptor descriptor);

    /// <summary>Returns all registered providers, in registration order.</summary>
    IReadOnlyList<ProviderDescriptor> GetAll();

    /// <summary>Returns providers filtered by category.</summary>
    IReadOnlyList<ProviderDescriptor> GetByCategory(ProviderCategory category);

    /// <summary>Tries to resolve one provider by key.</summary>
    bool TryGet(string key, [NotNullWhen(true)] out ProviderDescriptor? descriptor);
}
