using System.Diagnostics.CodeAnalysis;
using Sunfish.Foundation.Extensibility;

namespace Sunfish.Foundation.Catalog.ExtensionFields;

/// <summary>
/// Registry of extension fields per entity type. Persistence adapters,
/// UI renderers, and validators all consume this catalog rather than
/// discovering fields ad hoc.
/// </summary>
public interface IExtensionFieldCatalog
{
    /// <summary>Registers an extension field on the given entity type. Duplicate keys throw.</summary>
    void Register(Type entityType, ExtensionFieldSpec spec);

    /// <summary>Returns every registered spec for the entity type, in registration order.</summary>
    IReadOnlyList<ExtensionFieldSpec> GetFields(Type entityType);

    /// <summary>Tries to resolve one registered spec by key.</summary>
    bool TryGetField(Type entityType, ExtensionFieldKey key, [NotNullWhen(true)] out ExtensionFieldSpec? spec);
}
