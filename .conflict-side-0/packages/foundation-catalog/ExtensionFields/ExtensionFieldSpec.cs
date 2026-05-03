using Sunfish.Foundation.Extensibility;

namespace Sunfish.Foundation.Catalog.ExtensionFields;

/// <summary>
/// Describes one registered extension field on a canonical entity.
/// Specs are registered in an <see cref="IExtensionFieldCatalog"/> and drive
/// persistence, validation, and UI generation.
/// </summary>
/// <param name="Key">Case-sensitive field identifier, unique within an entity.</param>
/// <param name="ValueType">CLR type of the field's value. Primitives, strings, enums, or records.</param>
/// <param name="Scope">Who owns the field definition (bundle vs. tenant).</param>
/// <param name="Storage">Where the value is persisted (JSON bag vs. promoted column).</param>
/// <param name="IsRequired">If true, persistence should reject entities without a value.</param>
/// <param name="IsSearchable">Hint to the persistence adapter that this field should be indexed.</param>
/// <param name="DisplayName">Optional human-readable label.</param>
/// <param name="Description">Optional long-form description.</param>
public sealed record ExtensionFieldSpec(
    ExtensionFieldKey Key,
    Type ValueType,
    ExtensionFieldScope Scope,
    ExtensionStorage Storage,
    bool IsRequired = false,
    bool IsSearchable = false,
    string? DisplayName = null,
    string? Description = null);
