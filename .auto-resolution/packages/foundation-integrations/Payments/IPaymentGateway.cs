using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Integrations.Payments;

/// <summary>
/// Egress contract for payment authorization + capture + refund. W#19
/// Phase 6 introduces this stub mirroring the addendum's Money / ThreadId
/// stub pattern; ADR 0051 Stage 06 (W#5 substrate) will extend with the
/// full payment-orchestration surface (idempotency keys, dispute
/// handling, multi-party routing, etc.).
/// </summary>
public interface IPaymentGateway
{
    /// <summary>Authorize a payment (pre-capture). Returns a handle for later capture or refund.</summary>
    Task<PaymentAuthorizationResult> AuthorizeAsync(PaymentAuthorizationRequest request, CancellationToken ct);

    /// <summary>Capture a previously-authorized payment. Idempotent on the same handle.</summary>
    Task<PaymentCaptureResult> CaptureAsync(string authorizationHandle, CancellationToken ct);

    /// <summary>Refund a captured payment.</summary>
    Task<PaymentRefundResult> RefundAsync(string authorizationHandle, Money amount, CancellationToken ct);
}

/// <summary>Authorization request.</summary>
/// <param name="Tenant">Owning tenant (per <c>IMustHaveTenant</c>).</param>
/// <param name="Amount">Amount to authorize.</param>
/// <param name="CorrelationId">Caller-supplied correlation id (e.g., <c>WorkOrderId</c>).</param>
public sealed record PaymentAuthorizationRequest(
    TenantId Tenant,
    Money Amount,
    string CorrelationId);

/// <summary>Authorization result.</summary>
/// <param name="AuthorizationHandle">Provider-returned handle for later capture or refund.</param>
/// <param name="Status">Initial status — typically <see cref="PaymentStatus.Authorized"/>.</param>
public sealed record PaymentAuthorizationResult(string AuthorizationHandle, PaymentStatus Status);

/// <summary>Capture result.</summary>
/// <param name="Status">Post-capture status.</param>
public sealed record PaymentCaptureResult(PaymentStatus Status);

/// <summary>Refund result.</summary>
/// <param name="Status">Post-refund status.</param>
public sealed record PaymentRefundResult(PaymentStatus Status);

/// <summary>Lifecycle status of a payment (subset of ADR 0051 Stage 06's full vocabulary).</summary>
public enum PaymentStatus
{
    /// <summary>Authorized; not yet captured.</summary>
    Authorized,

    /// <summary>Captured; funds moved.</summary>
    Captured,

    /// <summary>Captured + refunded.</summary>
    Refunded,

    /// <summary>Authorization or capture failed at the gateway boundary.</summary>
    Failed
}
