using Sunfish.Blocks.PublicListings.Services;

namespace Sunfish.Blocks.PropertyLeasingPipeline.Services;

/// <summary>
/// W#22 Phase 5 — leasing-pipeline-side validation that runs after the
/// Bridge route's 5-layer abuse defense (W#28 Phase 5a). The validator
/// confirms the inquiry references a real, Published listing under the
/// correct tenant and that the email parses cleanly.
/// </summary>
/// <remarks>
/// The 5-layer defense (CAPTCHA + rate limit + email format + DNS MX +
/// scorer/triage in Phase 5b) catches abuse vectors at the Bridge
/// boundary; this validator catches *domain* invariants that only the
/// leasing pipeline can verify (listing-existence, tenant-match,
/// publication-state). Splitting the two means the Bridge doesn't
/// need a project reference into <c>blocks-public-listings</c>'s
/// repository — the leasing pipeline owns that.
/// </remarks>
public interface IInquiryValidator
{
    /// <summary>
    /// Validates <paramref name="request"/> against the leasing-pipeline
    /// invariants. Returns <see cref="InquiryValidationResult.Pass"/> on
    /// accept; otherwise the rejecting <see cref="InquiryValidationFailure"/>.
    /// </summary>
    Task<InquiryValidationResult> ValidateAsync(PublicInquiryRequest request, CancellationToken ct);
}

/// <summary>
/// Default <see cref="IInquiryValidator"/> backed by an
/// <see cref="IListingRepository"/>. Confirms (a) the listing exists,
/// (b) it's Published (Anonymous browsers cannot inquire on Draft or
/// Unlisted), (c) the request's tenant matches the listing's tenant,
/// (d) the email parses as a <c>MailAddress</c>.
/// </summary>
public sealed class DefaultInquiryValidator : IInquiryValidator
{
    private readonly IListingRepository _listings;

    /// <summary>Creates a validator that resolves listings via <paramref name="listings"/>.</summary>
    public DefaultInquiryValidator(IListingRepository listings)
    {
        ArgumentNullException.ThrowIfNull(listings);
        _listings = listings;
    }

    /// <inheritdoc />
    public async Task<InquiryValidationResult> ValidateAsync(PublicInquiryRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();

        if (!TryParseEmail(request.ProspectEmail))
        {
            return InquiryValidationResult.Fail(InquiryValidationFailure.EmailFormat,
                "Email failed format validation.");
        }

        var listing = await _listings.GetAsync(request.Tenant, request.Listing, ct).ConfigureAwait(false);
        if (listing is null)
        {
            return InquiryValidationResult.Fail(InquiryValidationFailure.ListingNotFound,
                $"Listing '{request.Listing.Value}' not found in tenant '{request.Tenant.Value}'.");
        }

        if (listing.Tenant != request.Tenant)
        {
            return InquiryValidationResult.Fail(InquiryValidationFailure.TenantMismatch,
                "Listing tenant does not match request tenant.");
        }

        if (listing.Status != Sunfish.Blocks.PublicListings.Models.PublicListingStatus.Published)
        {
            return InquiryValidationResult.Fail(InquiryValidationFailure.ListingNotPublished,
                $"Listing is in status '{listing.Status}', not Published.");
        }

        return InquiryValidationResult.Pass;
    }

    private static bool TryParseEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }
        try
        {
            _ = new System.Net.Mail.MailAddress(email);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

/// <summary>The verdict for an <see cref="IInquiryValidator.ValidateAsync"/> consultation.</summary>
public sealed record InquiryValidationResult
{
    /// <summary>Whether the request passed every check.</summary>
    public required bool Passed { get; init; }

    /// <summary>The category of failure when <see cref="Passed"/> is false.</summary>
    public InquiryValidationFailure? FailedAt { get; init; }

    /// <summary>Human-readable reason; null when <see cref="Passed"/> is true.</summary>
    public string? Reason { get; init; }

    /// <summary>The accept verdict.</summary>
    public static InquiryValidationResult Pass { get; } = new() { Passed = true };

    /// <summary>Builds a fail verdict.</summary>
    public static InquiryValidationResult Fail(InquiryValidationFailure failure, string reason) =>
        new() { Passed = false, FailedAt = failure, Reason = reason };
}

/// <summary>Failure categories for inquiry validation.</summary>
public enum InquiryValidationFailure
{
    /// <summary>The supplied email did not parse as a <c>MailAddress</c>.</summary>
    EmailFormat,

    /// <summary>The listing referenced by the inquiry was not found in the tenant.</summary>
    ListingNotFound,

    /// <summary>The request tenant did not match the listing's tenant.</summary>
    TenantMismatch,

    /// <summary>The listing exists but is not in the Published status.</summary>
    ListingNotPublished,
}

/// <summary>Thrown by <see cref="IPublicInquiryService.SubmitInquiryAsync"/> when validation fails.</summary>
public sealed class InquiryValidationException : InvalidOperationException
{
    /// <summary>The failure category from <see cref="IInquiryValidator"/>.</summary>
    public InquiryValidationFailure Failure { get; }

    /// <summary>Builds the exception with a categorical failure + reason.</summary>
    public InquiryValidationException(InquiryValidationFailure failure, string message) : base(message)
    {
        Failure = failure;
    }
}
