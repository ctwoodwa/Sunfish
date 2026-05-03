using System.Text.Json;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Assets.Audit;

/// <summary>
/// Operational input to <see cref="IAuditLog.AppendAsync"/>. The log fills in
/// <see cref="AuditRecord.Id"/>, <see cref="AuditRecord.Prev"/>, and
/// <see cref="AuditRecord.Hash"/>.
/// </summary>
public sealed record AuditAppend(
    EntityId EntityId,
    VersionId? VersionId,
    Op Op,
    ActorId Actor,
    TenantId Tenant,
    DateTimeOffset At,
    JsonDocument Payload,
    string? Justification = null,
    byte[]? Signature = null);
