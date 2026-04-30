using Sunfish.Blocks.PropertyLeasingPipeline.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.PropertyLeasingPipeline.Services;

/// <summary>
/// Operator-side decisioning contract — accepts ONLY
/// <see cref="DecisioningFacts"/>. The structural quarantine is the
/// FHA defense per ADR 0057: any decisioning code that attempts to read
/// <see cref="DemographicProfile"/> would have to take a different
/// dependency, making the breach observable to a reviewer.
/// </summary>
/// <remarks>
/// Phase 1 ships the contract. Phase 2 wires <c>InMemoryLeasingPipelineService</c>
/// to dispatch <see cref="ApplicationStatus.AwaitingDecision"/> applications
/// to whichever <see cref="IApplicationDecisioner"/> is registered.
/// </remarks>
public interface IApplicationDecisioner
{
    /// <summary>
    /// Decides whether to Accept or Decline an application based on
    /// <paramref name="facts"/> + the optional <paramref name="backgroundCheck"/>.
    /// MUST NOT consult <see cref="DemographicProfile"/>.
    /// </summary>
    /// <param name="applicationId">Application under decision.</param>
    /// <param name="facts">Non-protected-class fields (income, credit, references).</param>
    /// <param name="backgroundCheck">Background-check report if completed; otherwise null.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<ApplicationDecision> DecideAsync(
        Models.ApplicationId applicationId,
        DecisioningFacts facts,
        BackgroundCheckResult? backgroundCheck,
        CancellationToken ct);
}

/// <summary>Result of an <see cref="IApplicationDecisioner.DecideAsync"/> call.</summary>
public sealed record ApplicationDecision
{
    /// <summary>Whether the application was accepted.</summary>
    public required bool Accepted { get; init; }

    /// <summary>The actor recording the decision.</summary>
    public required ActorId DecidedBy { get; init; }

    /// <summary>Operator-supplied rationale; non-protected-class.</summary>
    public required string Reason { get; init; }

    /// <summary>Cited findings if <see cref="Accepted"/> is false AND a BG-check report contributed; required for FCRA <see cref="AdverseActionNotice"/>.</summary>
    public IReadOnlyList<AdverseFinding>? CitedFindings { get; init; }
}
