using System.Net.Mail;
using Sunfish.Foundation.Integrations.Captcha;

namespace Sunfish.Blocks.PublicListings.Defense;

/// <summary>
/// Default <see cref="IInquiryFormDefense"/> wiring Layers 1–3
/// (CAPTCHA + per-IP/per-tenant rate limit + email format + DNS MX).
/// Layers 4–5 are no-ops in Phase 5a; Phase 5b wires the W#20
/// substrates (<c>IInboundMessageScorer</c> + <c>IUnroutedTriageQueue</c>
/// per ADR 0052) once the cross-substrate adaptation lands.
/// </summary>
public sealed class InquiryFormDefense : IInquiryFormDefense
{
    private readonly ICaptchaVerifier _captcha;
    private readonly IInquiryRateLimiter _rateLimiter;
    private readonly IEmailMxResolver _mxResolver;

    /// <summary>Creates the orchestrator with the three Phase 5a layer dependencies.</summary>
    public InquiryFormDefense(
        ICaptchaVerifier captcha,
        IInquiryRateLimiter rateLimiter,
        IEmailMxResolver mxResolver)
    {
        ArgumentNullException.ThrowIfNull(captcha);
        ArgumentNullException.ThrowIfNull(rateLimiter);
        ArgumentNullException.ThrowIfNull(mxResolver);
        _captcha = captcha;
        _rateLimiter = rateLimiter;
        _mxResolver = mxResolver;
    }

    /// <inheritdoc />
    public async Task<InquiryDefenseResult> EvaluateAsync(InquiryFormSubmission submission, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(submission);

        // Layer 1 — CAPTCHA verify.
        var captchaResult = await _captcha.VerifyAsync(submission.CaptchaToken, submission.ClientIp, ct).ConfigureAwait(false);
        if (!captchaResult.Passed)
        {
            return InquiryDefenseResult.Fail(
                InquiryDefenseLayer.Captcha,
                $"CAPTCHA rejected (provider={captchaResult.Provider}, score={captchaResult.Score:0.00}).");
        }

        // Layer 2 — per-IP + per-tenant rate limit.
        var rateVerdict = await _rateLimiter.CheckAsync(submission.ClientIp, submission.Tenant, submission.ReceivedAt, ct).ConfigureAwait(false);
        if (!rateVerdict.Allowed)
        {
            return InquiryDefenseResult.Fail(
                InquiryDefenseLayer.RateLimit,
                $"Rate limit exceeded ({rateVerdict.ExceededWindow}).");
        }

        // Layer 3 — email format + DNS MX.
        if (!TryParseEmail(submission.ProspectEmail, out var domain))
        {
            return InquiryDefenseResult.Fail(
                InquiryDefenseLayer.EmailFormatAndMx,
                "Email failed format validation.");
        }
        var hasMx = await _mxResolver.HasMxRecordAsync(domain, ct).ConfigureAwait(false);
        if (!hasMx)
        {
            return InquiryDefenseResult.Fail(
                InquiryDefenseLayer.EmailFormatAndMx,
                $"Domain '{domain}' publishes no MX record.");
        }

        // Layers 4–5 deferred to Phase 5b (cross-substrate adaptation).
        return InquiryDefenseResult.Pass;
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
