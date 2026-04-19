namespace Sunfish.Foundation.Catalog.ExtensionFields;

/// <summary>
/// Declares how a registered extension field is stored at the persistence boundary.
/// </summary>
public enum ExtensionStorage
{
    /// <summary>Serialized into the entity's extension JSON column. Default.</summary>
    Json = 0,

    /// <summary>Promoted to a real table column via a persistence adapter. Enables indexing and typed querying.</summary>
    PromotedColumn = 1,
}
