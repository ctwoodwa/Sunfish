using Sunfish.Foundation.Integrations.Messaging;

namespace Sunfish.Blocks.Messaging.Services;

/// <summary>
/// In-memory no-op <see cref="IMessagingGateway"/> stub for tests and
/// host-bootstrap scenarios that don't yet wire a real provider adapter.
/// Returns <see cref="OutboundMessageStatus.Sent"/> immediately and emits
/// no audit. Provider-side implementations live in <c>providers-*</c>
/// packages per ADR 0013 provider-neutrality.
/// </summary>
public sealed class InMemoryMessagingGateway : IMessagingGateway
{
    /// <inheritdoc />
    public Task<OutboundMessageResult> SendAsync(OutboundMessageRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var providerMessageId = $"in-memory:{request.Id}";
        return Task.FromResult(new OutboundMessageResult(request.Id, providerMessageId, OutboundMessageStatus.Sent));
    }

    /// <inheritdoc />
    public Task<OutboundMessageStatus> GetStatusAsync(string providerMessageId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerMessageId);
        return Task.FromResult(OutboundMessageStatus.Sent);
    }
}
