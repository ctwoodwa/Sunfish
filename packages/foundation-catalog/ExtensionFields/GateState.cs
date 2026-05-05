namespace Sunfish.Foundation.Catalog.ExtensionFields;

/// <summary>Outcome of feature-gate evaluation for one <see cref="ExtensionFieldSpec"/>.</summary>
public enum GateState
{
    /// <summary>Spec has no <see cref="ExtensionFieldSpec.FeatureKey"/>; field is unconditionally visible.</summary>
    Ungated = 0,

    /// <summary>Spec has a FeatureKey and the gate evaluated ON.</summary>
    GatedOn = 1,

    /// <summary>
    /// Spec is gated OFF and policy is <see cref="FeatureGateOffPolicy.Hide"/>; field is
    /// excluded from materialization but data is preserved. Currently never returned by
    /// <c>IExtensionFieldCatalog.GetFieldsAsync</c>; see ADR 0075 §Open questions item 5.
    /// </summary>
    Hidden = 2,

    /// <summary>
    /// Spec is gated OFF and policy is <see cref="FeatureGateOffPolicy.Sequester"/>; field is
    /// excluded but tracked in <c>ISequestrationStore</c>. Currently never returned by
    /// <c>IExtensionFieldCatalog.GetFieldsAsync</c>; see ADR 0075 §Open questions item 5.
    /// </summary>
    Sequestered = 3,

    /// <summary>
    /// Spec is gated OFF and policy is <see cref="FeatureGateOffPolicy.Redact"/>; underlying
    /// data has been tombstoned. Currently never returned by
    /// <c>IExtensionFieldCatalog.GetFieldsAsync</c>; see ADR 0075 §Open questions item 5.
    /// </summary>
    Redacted = 4,
}
