using System.Net.Mail;
using Sunfish.Blocks.PublicListings.Audit;
using Sunfish.Blocks.PublicListings.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Integrations.Captcha;
using Sunfish.Kernel.Audit;

namespace Sunfish.Blocks.PublicListings.Defense;

/// <summary>
/// Default <see cref="IInquiryFormDefense"/> wiring Layers 1–3
/// (CAPTCHA + per-IP/per-tenant rate limit + email format + DNS MX).
/// Layers 4–5 are no-ops in Phase 5a; Phase 5b wires the W#20
/// substrates (<c>IInboundMessageScorer</c> + <c>IUnroutedTriageQueue</c>
/// per ADR 0052) once the cross-substrate adaptation lands.
/// W#28 Phase 7: when a <see cref="PublicListingAuditEmitter"/> is
/// supplied, every rejection emits <see cref="AuditEventType.InquiryRejected"/>
/// with the rejecting layer + reason in the body for forensic
/// reconstruction.
/// </summary>
public sealed class InquiryFormDefense : IInquiryFormDefense
{
    private readonly ICaptchaVerifier _captcha;
    private readonly IInquiryRateLimiter _rateLimiter;
    private readonly IEmailMxResolver _mxResolver;
    private readonly PublicListingAuditEmitter? _audit;

    /// <summary>Creates the orchestrator with the three Phase 5a layer dependencies; audit emission disabled.</summary>
    public InquiryFormDefense(
        ICaptchaVerifier captcha,
        IInquiryRateLimiter rateLimiter,
        IEmailMxResolver mxResolver)
        : this(captcha, rateLimiter, mxResolver, audit: null) { }

    /// <summary>Creates the orchestrator with optional audit emission (W#28 Phase 7).</summary>
    public InquiryFormDefense(
        ICaptchaVerifier captcha,
        IInquiryRateLimiter rateLimiter,
        IEmailMxResolver mxResolver,
        PublicListingAuditEmitter? audit)
    {
        ArgumentNullException.ThrowIfNull(captcha);
        ArgumentNullException.ThrowIfNull(rateLimiter);
        ArgumentNullException.ThrowIfNull(mxResolver);
        _captcha = captcha;
        _rateLimiter = rateLimiter;
        _mxResolver = mxResolver;
        _audit = audit;
    }

    /// <inheritdoc />
    public async Task<InquiryDefenseResult> EvaluateAsync(InquiryFormSubmission submission, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(submission);

        // Layer 1 — CAPTCHA verify.
        var captchaResult = await _captcha.VerifyAsync(submission.CaptchaToken, submission.ClientIp, ct).ConfigureAwait(false);
        if (!captchaResult.Passed)
        {
            return await RejectAsync(
                submission,
                InquiryDefenseLayer.Captcha,
                $"CAPTCHA rejected (provider={captchaResult.Provider}, score={captchaResult.Score:0.00}).",
                ct).ConfigureAwait(false);
        }

        // Layer 2 — per-IP + per-tenant rate limit.
        var rateVerdict = await _rateLimiter.CheckAsync(submission.ClientIp, submission.Tenant, submission.ReceivedAt, ct).ConfigureAwait(false);
        if (!rateVerdict.Allowed)
        {
            return await RejectAsync(
                submission,
                InquiryDefenseLayer.RateLimit,
                $"Rate limit exceeded ({rateVerdict.ExceededWindow}).",
                ct).ConfigureAwait(false);
        }

        // Layer 3 — email format + DNS MX.
        if (!TryParseEmail(submission.ProspectEmail, out var domain))
        {
            return await RejectAsync(
                submission,
                InquiryDefenseLayer.EmailFormatAndMx,
                "Email failed format validation.",
                ct).ConfigureAwait(false);
        }
        var hasMx = await _mxResolver.HasMxRecordAsync(domain, ct).ConfigureAwait(false);
        if (!hasMx)
        {
            return await RejectAsync(
                submission,
                InquiryDefenseLayer.EmailFormatAndMx,
                $"Domain '{domain}' publishes no MX record.",
                ct).ConfigureAwait(false);
        }

        // Layers 4–5 deferred to Phase 5b (cross-substrate adaptation).
        return InquiryDefenseResult.Pass;
    }

    private async Task<InquiryDefenseResult> RejectAsync(InquiryFormSubmission submission, InquiryDefenseLayer layer, string reason, CancellationToken ct)
    {
        if (_audit is not null)
        {
            // The defense doesn't know the listing id at this layer — the
            // route maps slug → listing upstream; audit captures tenant +
            // route-shape submission only. A "listing-unknown" sentinel
            // keeps the body shape stable for downstream filtering.
            await _audit.EmitAsync(
                AuditEventType.InquiryRejected,
                PublicListingAuditPayloadFactory.InquiryRejected(submission.Tenant, default, layer, reason),
                submission.ReceivedAt,
                ct).ConfigureAwait(false);
        }
        return InquiryDefenseResult.Fail(layer, reason);
    }

    private static bool TryParseEmail(string email, out string domain)
    {
        domain = string.Empty;
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }
        try
        {
            var parsed = new MailAddress(email);
            domain = parsed.Host;
            return !string.IsNullOrWhiteSpace(domain);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
