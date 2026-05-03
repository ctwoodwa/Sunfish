namespace Sunfish.Bridge.Listings;

/// <summary>
/// Route-local form-post primitive for the W#28 inquiry POST endpoint
/// (per ADR 0059 amendment A1). The route maps this primitive to
/// <c>Sunfish.Blocks.PropertyLeasingPipeline.Services.PublicInquiryRequest</c>
/// at the controller boundary; this primitive never crosses the block
/// boundary.
/// </summary>
/// <param name="Name">Prospect's display name.</param>
/// <param name="Email">Prospect's email — drives the verification flow.</param>
/// <param name="Phone">Optional phone for SMS callbacks.</param>
/// <param name="MessageBody">Free-text message body.</param>
/// <param name="CaptchaToken">CAPTCHA token verified at Layer 1 of the defense pipeline.</param>
public sealed record InquiryFormPost(
    string? Name,
    string? Email,
    string? Phone,
    string? MessageBody,
    string? CaptchaToken);
