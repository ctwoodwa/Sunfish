namespace Sunfish.Foundation.Assets.Audit;

/// <summary>
/// Identifier for a single audit record. Phase A uses a monotonic <c>long</c> with a
/// compact base32-lowercase string form so it's log-friendly.
/// </summary>
public readonly record struct AuditId(long Value)
{
    /// <inheritdoc />
    public override string ToString() => Value.ToString("D");
}
