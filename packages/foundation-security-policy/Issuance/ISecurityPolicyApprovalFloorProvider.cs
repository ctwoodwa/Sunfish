using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Ship.Common;
using Sunfish.Foundation.Wayfinder;

namespace Sunfish.Foundation.SecurityPolicy.Issuance;

/// <summary>
/// Evaluates the §3.1 floor invariants over a proposal + the in-flight
/// <see cref="ApprovalChain"/>. Per ADR 0068 §3.1.
/// </summary>
/// <remarks>
/// See §GC.1 in ADR 0068 (docs/adrs/0068-tenant-security-policy.md).
/// Enforcement behavior in this package intersects HIPAA, PCI-DSS,
/// SOC 2, GDPR, and the EU AI Act. The presets and defaults are
/// informed guidance, NOT legal advice. Deployers MUST obtain
/// qualified legal counsel before configuring enforcement behavior
/// for production use.
/// <para>
/// The 5 floor invariants are platform-mandatory — no Standing Order
/// may relax them. Registered via <c>AddSingleton</c>
/// (NOT <c>TryAddSingleton</c>) per §2.1.1, preventing plugin
/// shadowing. A consumer that wants to ADD constraints layers a
/// decorator over <see cref="DefaultSecurityPolicyApprovalFloorProvider"/>;
/// a consumer that wants to RELAX constraints cannot — the platform
/// floor is the canonical minimum.
/// </para>
/// </remarks>
public interface ISecurityPolicyApprovalFloorProvider
{
    /// <summary>
    /// Evaluate the §3.1 floor invariants against
    /// <paramref name="proposal"/> + <paramref name="chainSoFar"/>.
    /// Returns <see cref="ApprovalFloorVerdict.AllowApply"/>=<c>true</c>
    /// when all 5 invariants hold; otherwise returns a verdict with a
    /// non-null reason code + accessible message.
    /// </summary>
    /// <param name="proposal">The proposal whose approval chain is being evaluated.</param>
    /// <param name="proposer">The actor that issued the proposal (per §3.1 invariant 3, may not approve their own proposal).</param>
    /// <param name="chainSoFar">The approval steps recorded so far, in chronological order. Must include the in-flight approval being evaluated.</param>
    /// <param name="approverRoles">Per-approver <see cref="ShipRole"/> snapshot at evaluation time, sourced from the most recently Applied role-assignment Standing Order (per §3.1).</param>
    /// <param name="proofExpiriesByApprover">Per-approver <see cref="CapabilityProof"/> expiry timestamp; used for the §3.1 invariant 5 freshness check.</param>
    /// <param name="now">Current wall-clock time, supplied by the caller's <c>TimeProvider</c> for testability.</param>
    ApprovalFloorVerdict Evaluate(
        StandingOrderId proposal,
        ActorId proposer,
        ApprovalChain chainSoFar,
        System.Collections.Generic.IReadOnlyDictionary<ActorId, ShipRole> approverRoles,
        System.Collections.Generic.IReadOnlyDictionary<ActorId, System.DateTimeOffset> proofExpiriesByApprover,
        System.DateTimeOffset now);
}
