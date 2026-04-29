namespace Sunfish.Blocks.Maintenance.Models;

/// <summary>Stable identifier for a <see cref="WorkOrderCompletionAttestation"/>.</summary>
/// <param name="Value">Underlying GUID.</param>
public readonly record struct WorkOrderCompletionAttestationId(Guid Value)
{
    /// <summary>Mints a new id backed by a fresh GUID.</summary>
    public static WorkOrderCompletionAttestationId NewId() => new(Guid.NewGuid());

    /// <inheritdoc />
    public override string ToString() => Value.ToString("D");
}
