using Marilo.PmDemo.Data;
using Marilo.PmDemo.Data.Entities;
using Marilo.PmDemo.Messages;
using Wolverine;

namespace Marilo.PmDemo.Handlers;

public static class TaskStatusChangedHandler
{
    public static async Task<NotificationRequested> Handle(
        TaskStatusChangedEvent evt,
        PmDemoDbContext db,
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
}
