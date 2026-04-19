using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.TaxReporting.Models;

/// <summary>
/// A tax report record. Immutable after finalization; use <c>with</c> expressions for state transitions.
/// </summary>
/// <param name="Id">Unique identifier for this report.</param>
/// <param name="Year">The tax year this report covers.</param>
/// <param name="Kind">
/// Discriminant identifying the body subtype. Matches <see cref="Body"/>.<see cref="TaxReportBody.Kind"/>.
/// </param>
/// <param name="PropertyId">
/// Optional: scopes this report to a single property. <see langword="null"/> indicates an aggregate report.
/// </param>
/// <param name="Status">Current lifecycle status.</param>
/// <param name="GeneratedAtUtc">Timestamp when this report was first created.</param>
/// <param name="SignatureValue">
/// Hex-encoded SHA-256 hash over the canonical-JSON serialization of <see cref="Body"/>.
/// Populated by <c>FinalizeAsync</c>. This is a <strong>content hash</strong>, not a
/// cryptographic digital signature.
/// <br/>
/// TODO (future pass): replace with a real Ed25519 signature using Foundation's
/// <c>PrincipalId</c> + private-key facility once that primitive lands.
/// </param>
/// <param name="Body">The structured report body. Subtype is determined by <see cref="Kind"/>.</param>
public sealed record TaxReport(
    TaxReportId Id,
    TaxYear Year,
    TaxReportKind Kind,
    EntityId? PropertyId,
    TaxReportStatus Status,
    Instant GeneratedAtUtc,
    string? SignatureValue,
    TaxReportBody Body);
