using Sunfish.Foundation.Crypto;

namespace Sunfish.Kernel.Audit;

/// <summary>
/// A single attestation: a principal's signature over an audit record's
/// canonical bytes. Element type of <see cref="AuditRecord.AttestingSignatures"/>.
/// Pairing the principal with the signature lets downstream consumers
/// (compliance reviewers, regulatory exports) look up the attesting key
/// when verifying the attestation set.
/// </summary>
/// <param name="PrincipalId">The principal whose signature this is.</param>
/// <param name="Signature">Ed25519 signature over the canonical bytes the principal endorses.</param>
public readonly record struct AttestingSignature(PrincipalId PrincipalId, Signature Signature);
