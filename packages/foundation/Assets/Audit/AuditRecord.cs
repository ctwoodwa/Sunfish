using System.Text.Json;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Assets.Audit;

/// <summary>
/// A single append-only audit-log entry.
/// </summary>
/// <remarks>
/// Spec §3.3. <see cref="Signature"/> is nullable in Phase A (plan D-NULLABLE-SIGNATURES);
/// <see cref="Hash"/> chains each record to its predecessor using SHA-256.
/// </remarks>
public sealed record AuditRecord(
    AuditId Id,
    EntityId EntityId,
    VersionId? VersionId,
    Op Op,
    ActorId Actor,
    TenantId Tenant,
    DateTimeOffset At,
    string? Justification,
    JsonDocument Payload,
    byte[]? Signature,
    AuditId? Prev,
    string Hash);
