using Sunfish.Kernel.Security.Attestation;

namespace Sunfish.Kernel.Buckets;

/// <summary>
/// Holds the set of known bucket definitions and answers eligibility questions at
/// capability-negotiation time (paper §10.2: "Bucket eligibility is evaluated at capability
/// negotiation. The sync daemon constructs a minimal subscription set from peer attestations.").
/// </summary>
public interface IBucketRegistry
{
    /// <summary>All currently-registered bucket definitions.</summary>
    IReadOnlyList<BucketDefinition> Definitions { get; }

    /// <summary>Register a new bucket definition. Throws if a bucket with the same name already exists.</summary>
    /// <param name="bucket">The parsed definition (typically loaded via <see cref="IBucketYamlLoader"/>).</param>
    /// <exception cref="ArgumentNullException"><paramref name="bucket"/> is null.</exception>
    /// <exception cref="InvalidOperationException">A bucket with the same name is already registered.</exception>
    void Register(BucketDefinition bucket);

    /// <summary>Find a bucket by name. Returns null if unknown.</summary>
    /// <param name="name">The bucket name (case-sensitive, ordinal match).</param>
    BucketDefinition? Find(string name);

    /// <summary>
    /// Given a peer's presented role attestations, return the set of buckets that peer is
    /// eligible to subscribe to. Paper §10.2: "Non-eligible nodes never receive bucket events."
    /// </summary>
    /// <param name="peerAttestations">
    /// The collection of role attestations the peer presented during capability negotiation.
    /// Callers are expected to have verified each attestation (signature, expiry, issuer) via
    /// <see cref="IAttestationVerifier"/> before passing them here — this registry only matches
    /// <see cref="RoleAttestation.Role"/> strings and does not re-verify cryptographic claims.
    /// </param>
    /// <returns>Buckets whose <see cref="BucketDefinition.RequiredAttestation"/> is present in at least one supplied attestation.</returns>
    IReadOnlyList<BucketDefinition> EligibleBucketsFor(IReadOnlyList<RoleAttestation> peerAttestations);
}
