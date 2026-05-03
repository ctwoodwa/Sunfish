using System.Net;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.PublicListings.Defense;

/// <summary>
/// Server-side inquiry-form defense per ADR 0059 §"Inquiry-form abuse
/// posture (ADR 0043 T2 boundary)". The inquiry route on the Bridge
/// runs an inbound submission through this contract; only submissions
/// that pass all configured layers are forwarded to ADR 0057's
/// <c>IPublicInquiryService.SubmitInquiryAsync</c>.
/// </summary>
/// <remarks>
/// W#28 Phase 5a wires Layers 1–3 (CAPTCHA + per-IP/per-tenant rate
/// limit + email format + DNS MX). Layers 4–5
/// (<c>IInboundMessageScorer</c> + <c>IUnroutedTriageQueue</c> from W#20
/// per ADR 0052) require cross-substrate adaptation between
/// <c>PublicInquiryRequest</c> + <c>InboundMessageEnvelope</c> shapes;
/// XO has been beaconed to direct that work in Phase 5b. This service
/// keeps a place-holder for the W#20 hooks so Phase 5b lands without
/// re-shaping the contract.
/// </remarks>
public interface IInquiryFormDefense
{
    /// <summary>
    /// Runs all configured defense layers on <paramref name="submission"/>
    /// in fail-closed order. Returns <see cref="InquiryDefenseResult.Pass"/>
    /// only when every layer accepts; otherwise the first rejecting
    /// layer's verdict is returned.
    /// </summary>
    Task<InquiryDefenseResult> EvaluateAsync(InquiryFormSubmission submission, CancellationToken ct);
}

/// <summary>
/// Wire-shape of an inbound inquiry-form submission, populated by the
/// Bridge controller from its route-local <c>PublicInquiryFormPost</c>
/// primitive (per ADR 0059 amendment A1).
/// </summary>
public sealed record InquiryFormSubmission
{
    /// <summary>Owning tenant (resolved from listing slug at the Bridge layer).</summary>
    public required TenantId Tenant { get; init; }

    /// <summary>CAPTCHA token to verify in Layer 1.</summary>
    public required string CaptchaToken { get; init; }

    /// <summary>Source IP — drives Layer 1 + Layer 2 + audit.</summary>
    public required IPAddress ClientIp { get; init; }

    /// <summary>Email address claimed on the form — Layer 3 validates format + MX.</summary>
    public required string ProspectEmail { get; init; }

    /// <summary>Free-text body — passed to scorer in Layer 4 (Phase 5b).</summary>
    public required string MessageBody { get; init; }

    /// <summary>UTC timestamp captured at submission for audit + rate-limit windowing.</summary>
    public required DateTimeOffset ReceivedAt { get; init; }

    /// <summary>The inquirer's display name as captured on the form (Phase 5b synthetic envelope subject).</summary>
    public string? InquirerName { get; init; }

    /// <summary>Listing slug the inquiry targets (Phase 5b synthetic envelope subject).</summary>
    public string? ListingSlug { get; init; }

    /// <summary>Browser user-agent (Phase 5b synthetic envelope provider-header for downstream forensics).</summary>
    public string? UserAgent { get; init; }
}

/// <summary>Result of running an <see cref="InquiryFormSubmission"/> through the defense pipeline.</summary>
public sealed record InquiryDefenseResult
{
    /// <summary>Whether every configured layer accepted the submission.</summary>
    public required bool Passed { get; init; }

    /// <summary>The layer that rejected; null when <see cref="Passed"/> is true.</summary>
    public InquiryDefenseLayer? RejectedAt { get; init; }

    /// <summary>Human-readable rejection reason (audit body); null when <see cref="Passed"/> is true.</summary>
    public string? Reason { get; init; }

    /// <summary>The accept verdict.</summary>
    public static InquiryDefenseResult Pass { get; } = new() { Passed = true };

    /// <summary>Builds a fail verdict.</summary>
    public static InquiryDefenseResult Fail(InquiryDefenseLayer layer, string reason) =>
        new() { Passed = false, RejectedAt = layer, Reason = reason };
}

/// <summary>The 5 defense layers, in fail-closed evaluation order.</summary>
public enum InquiryDefenseLayer
{
    /// <summary>Layer 1: CAPTCHA token verification.</summary>
    Captcha = 1,

    /// <summary>Layer 2: per-IP + per-tenant sliding-window rate limit.</summary>
    RateLimit = 2,

    /// <summary>Layer 3: email format + DNS MX record check.</summary>
    EmailFormatAndMx = 3,

    /// <summary>Layer 4: <c>IInboundMessageScorer</c> abuse heuristic (W#20; Phase 5b).</summary>
    AbuseScore = 4,

    /// <summary>Layer 5: manual <c>IUnroutedTriageQueue</c> hold (W#20; Phase 5b).</summary>
    ManualTriage = 5,
}
