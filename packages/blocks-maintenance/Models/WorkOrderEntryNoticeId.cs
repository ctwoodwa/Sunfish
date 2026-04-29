namespace Sunfish.Blocks.Maintenance.Models;

/// <summary>Stable identifier for a <see cref="WorkOrderEntryNotice"/>.</summary>
/// <param name="Value">Underlying GUID.</param>
public readonly record struct WorkOrderEntryNoticeId(Guid Value)
{
    /// <summary>Mints a new id backed by a fresh GUID.</summary>
    public static WorkOrderEntryNoticeId NewId() => new(Guid.NewGuid());

    /// <inheritdoc />
    public override string ToString() => Value.ToString("D");
}
