using System.Collections.Concurrent;

namespace Sunfish.Foundation.Integrations.Payments;

/// <summary>
/// W#19 Phase 6 stub <see cref="IPaymentGateway"/>. Returns successful
/// authorizations / captures / refunds without contacting any provider.
/// Records every call in an in-memory journal for assertion in tests.
/// **NOT for production** — ADR 0051 Stage 06 will replace with the real
/// provider-backed gateway.
/// </summary>
public sealed class InMemoryPaymentGateway : IPaymentGateway
{
    private readonly ConcurrentDictionary<string, PaymentJournalEntry> _journal = new();

    /// <summary>Snapshot of authorizations / captures / refunds for test assertions.</summary>
    public IReadOnlyDictionary<string, PaymentJournalEntry> Journal => _journal;

    /// <inheritdoc />
    public Task<PaymentAuthorizationResult> AuthorizeAsync(PaymentAuthorizationRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var handle = $"in-memory-auth:{Guid.NewGuid():N}";
        _journal[handle] = new PaymentJournalEntry(request, PaymentStatus.Authorized, RefundedAmount: null);
        return Task.FromResult(new PaymentAuthorizationResult(handle, PaymentStatus.Authorized));
    }

    /// <inheritdoc />
    public Task<PaymentCaptureResult> CaptureAsync(string authorizationHandle, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(authorizationHandle);
        if (!_journal.TryGetValue(authorizationHandle, out var entry))
        {
            return Task.FromResult(new PaymentCaptureResult(PaymentStatus.Failed));
        }
        if (entry.Status == PaymentStatus.Captured)
        {
            // Idempotent — re-capture on already-captured returns the captured status.
            return Task.FromResult(new PaymentCaptureResult(PaymentStatus.Captured));
        }
        _journal[authorizationHandle] = entry with { Status = PaymentStatus.Captured };
        return Task.FromResult(new PaymentCaptureResult(PaymentStatus.Captured));
    }

    /// <inheritdoc />
    public Task<PaymentRefundResult> RefundAsync(string authorizationHandle, Money amount, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(authorizationHandle);
        if (!_journal.TryGetValue(authorizationHandle, out var entry))
        {
            return Task.FromResult(new PaymentRefundResult(PaymentStatus.Failed));
        }
        if (entry.Status != PaymentStatus.Captured)
        {
            return Task.FromResult(new PaymentRefundResult(PaymentStatus.Failed));
        }
        _journal[authorizationHandle] = entry with { Status = PaymentStatus.Refunded, RefundedAmount = amount };
        return Task.FromResult(new PaymentRefundResult(PaymentStatus.Refunded));
    }
}

/// <summary>One row in <see cref="InMemoryPaymentGateway"/>'s journal — captures a request + its current status.</summary>
public sealed record PaymentJournalEntry(PaymentAuthorizationRequest Request, PaymentStatus Status, Money? RefundedAmount);
