namespace Sunfish.Foundation.Catalog.ExtensionFields;

/// <summary>
/// Result of <c>IExtensionFieldCatalog.GetFieldsAsync</c>: a registered spec
/// plus the gate-evaluation outcome. Consumers switch on
/// <see cref="GateState"/> to decide whether to surface the field
/// (<see cref="GateState.Ungated"/> or <see cref="GateState.GatedOn"/>) or
/// skip it. Per ADR 0075.
/// </summary>
public sealed record MaterializedExtensionField(
    ExtensionFieldSpec Spec,
    GateState GateState);
