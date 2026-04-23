using Sunfish.Bridge.Data;
using Sunfish.Bridge.Data.Entities;
using Sunfish.Bridge.Messages;
using Wolverine;

namespace Sunfish.Bridge.Handlers;

public static class TaskStatusChangedHandler
{
    // Legacy PM-domain handler. AuditRecord is marked Obsolete per ADR 0031 — the
    // audit trail for task events moves to the per-tenant data plane (Wave 5.2).
    // Suppressed inline so existing Wolverine wiring keeps compiling during the refactor.
#pragma warning disable CS0618
    public static async Task<NotificationRequested> Handle(
        TaskStatusChangedEvent evt,
        SunfishBridgeDbContext db,
        CancellationToken ct)
    {
        db.AuditRecords.Add(new AuditRecord
        {
            TenantId = evt.TenantId,
            ActorId = evt.ActorId,
            ResourceType = "Task",
            ResourceId = evt.TaskId.ToString(),
            Action = "StatusChanged",
            Before = evt.FromStatus,
            After = evt.ToStatus,
        });
        await db.SaveChangesAsync(ct);

        return new NotificationRequested(
            UserId: evt.ActorId,
            TenantId: evt.TenantId,
            Channel: "in-app",
            Subject: "Task status changed",
            Body: $"Task {evt.TaskId} moved from {evt.FromStatus} to {evt.ToStatus}.");
    }
#pragma warning restore CS0618
}
