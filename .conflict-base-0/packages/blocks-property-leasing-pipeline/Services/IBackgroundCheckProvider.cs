using Sunfish.Blocks.PropertyLeasingPipeline.Models;

namespace Sunfish.Blocks.PropertyLeasingPipeline.Services;

/// <summary>
/// Pluggable background-check provider. The first concrete adapter
/// (e.g. <c>providers-checkr</c>, <c>providers-transunion</c>) is a
/// follow-up package per ADR 0013; W#22 Phase 3 ships only the contract
/// + an InMemory provider for test/demo scenarios.
/// </summary>
public interface IBackgroundCheckProvider
{
    /// <summary>
    /// Initiates a background check for an application. Returns a
    /// pending result (<see cref="BackgroundCheckOutcome.Clear"/> or
    /// <see cref="BackgroundCheckOutcome.HasFindings"/> when the
    /// provider answers synchronously; <see cref="BackgroundCheckOutcome.Error"/>
    /// on provider error).
    /// </summary>
    Task<BackgroundCheckResult> KickOffAsync(BackgroundCheckRequest request, CancellationToken ct);

    /// <summary>
    /// Polls a previously-initiated check by its <paramref name="vendorRef"/>
    /// for status updates. Used when <see cref="KickOffAsync"/> returns
    /// before the provider has the report ready.
    /// </summary>
    Task<BackgroundCheckResult> GetStatusAsync(string vendorRef, CancellationToken ct);
}

/// <summary>Submission shape for <see cref="IBackgroundCheckProvider.KickOffAsync"/>.</summary>
public sealed record BackgroundCheckRequest
{
    /// <summary>The application this check is for.</summary>
    public required ApplicationId Application { get; init; }

    /// <summary>Applicant's full legal name as supplied on the application.</summary>
    public required string ApplicantFullName { get; init; }

    /// <summary>Applicant's date of birth (DOB is required for most CRA queries).</summary>
    public required DateOnly DateOfBirth { get; init; }

    /// <summary>SSN-suffix or full SSN per CRA agreement; provider adapters MUST quarantine + redact in logs.</summary>
    public required string SocialSecurityIdentifier { get; init; }

    /// <summary>Applicant's primary residence state (drives jurisdiction-specific FCRA addenda).</summary>
    public required string PrimaryResidenceState { get; init; }
}
