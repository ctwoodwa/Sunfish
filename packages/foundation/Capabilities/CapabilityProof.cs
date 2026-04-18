using Sunfish.Foundation.Crypto;

namespace Sunfish.Foundation.Capabilities;

/// <summary>
/// A self-contained, transferable proof that <paramref name="Subject"/> holds
/// <paramref name="Action"/> on <paramref name="Resource"/> as of <paramref name="ProvedAt"/>.
/// </summary>
/// <remarks>
/// The proof is the ordered chain of signed operations a verifier must replay to reach
/// the conclusion. Any recipient that trusts the root principal(s) can verify the proof
/// offline without contacting the originating graph.
/// </remarks>
/// <param name="Subject">The principal the capability is being proved for.</param>
/// <param name="Resource">The resource the capability applies to.</param>
/// <param name="Action">The action being proved.</param>
/// <param name="OpChain">The ordered signed operations that justify the capability.</param>
/// <param name="ProvedAt">The effective time at which the proof is valid.</param>
public sealed record CapabilityProof(
    PrincipalId Subject,
    Resource Resource,
    CapabilityAction Action,
    IReadOnlyList<SignedOperation<CapabilityOp>> OpChain,
    DateTimeOffset ProvedAt);
