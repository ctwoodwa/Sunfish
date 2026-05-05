namespace Sunfish.Foundation.Catalog.ExtensionFields;

/// <summary>
/// Policy applied when an <see cref="ExtensionFieldSpec.FeatureKey"/> evaluates
/// to OFF. Default is <see cref="Hide"/>; <see cref="Sequester"/> composes
/// the W#35 / ADR 0028-A5.4 sequestration partition; <see cref="Redact"/> is
/// destructive and requires explicit operator capability per ADR 0046.
/// </summary>
public enum FeatureGateOffPolicy
{
    /// <summary>
    /// Default. Field is excluded from materialization; underlying data is preserved
    /// in storage but not surfaced. Reversible: when the gate flips ON, the field
    /// reappears.
    /// </summary>
    Hide = 0,

    /// <summary>
    /// Field is excluded from materialization AND its underlying data is registered
    /// with <c>Sunfish.Foundation.Migration.ISequestrationStore</c> (W#35). Composes
    /// ADR 0028-A5.4. Reversible.
    /// </summary>
    Sequester = 1,

    /// <summary>
    /// Field is excluded from materialization AND its underlying data is destroyed
    /// (tombstoned). Requires explicit operator capability and emits a destructive
    /// audit record. NOT reversible.
    /// </summary>
    Redact = 2,
}
