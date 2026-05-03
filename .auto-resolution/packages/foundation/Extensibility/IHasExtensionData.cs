namespace Sunfish.Foundation.Extensibility;

/// <summary>
/// Marks an entity as extension-capable. Extension fields are declared
/// separately through an extension-field catalog; this interface only
/// exposes the per-instance bag.
/// </summary>
public interface IHasExtensionData
{
    /// <summary>The per-entity extension bag. Never null.</summary>
    ExtensionDataBag Extensions { get; }
}
