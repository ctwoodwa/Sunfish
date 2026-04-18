namespace Marilo.PmDemo.Messages;

public record TaskStatusChangedEvent(
    Guid TaskId,
    string FromStatus,
    string ToStatus,
    string ActorId,
    string TenantId);

public record NotificationRequested(
    string UserId,
    string TenantId,
    string Channel,
    string Subject,
    string Body);
