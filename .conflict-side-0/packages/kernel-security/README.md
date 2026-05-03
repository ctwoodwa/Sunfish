# Sunfish.Kernel.Security

Role attestation and per-role symmetric-key distribution for the Sunfish local-node architecture.

This package implements [paper](../../_shared/product/local-node-architecture-paper.md) **§11.3 —
Role Attestation vs. Key Distribution**. It is the interior format of the opaque
`attestation_bundle` carried on the wire by the [sync daemon protocol](../../docs/specifications/sync-daemon-protocol.md)
§3.2 / §5.

Wave 1.6 of [paper-alignment-plan.md](../../_shared/product/paper-alignment-plan.md).

## Two flows, one package

Paper §11.3 is explicit that *role membership* and *encryption keys* are two
different things:

1. **Role attestation** — the admin issues a signed claim ("node X holds role R in
   team T until time E"). The attestation proves eligibility for sync-capability
   negotiation. It does **not** carry an encryption key.
2. **Key distribution** — the admin separately generates a per-role symmetric key,
   wraps it individually for each qualifying member using their public key, and
   publishes the wrapped bundles as administrative events. Each member unwraps
   with its private key and stores the result in the OS keystore.

Key rotation is a re-run of flow (2) that omits revoked members; because the
sync daemon's subscription decision is independent of key possession, revoked
members continue to sync but can no longer decrypt new field-level ciphertext.

## Primitives

| Surface | Purpose | Algorithm |
|---|---|---|
| `IEd25519Signer` | Attestation signatures | Ed25519 (NSec.Cryptography) |
| `IX25519KeyAgreement` | Sealed-box for role-key wrapping | X25519 + HKDF-SHA256 + ChaCha20-Poly1305 |
| `IAttestationIssuer` / `IAttestationVerifier` | Role attestations | Signs canonical CBOR |
| `IRoleKeyManager` | Per-role key lifecycle | Generate → wrap → unwrap → store |
| `AttestationBundle` | CBOR wire envelope | RFC 8949 §4.2 canonical |

## Dependencies

- **Sunfish.Foundation** — shared types.
- **Sunfish.UICore** — kernel-facade cross-link.
- **Sunfish.Kernel** — kernel-facade cross-link.
- **Sunfish.Foundation.LocalFirst** — supplies `IKeystore` for role-key caching.
- **NSec.Cryptography** — Ed25519 and X25519 primitives (matches Foundation.Crypto).
- **System.Formats.Cbor** — canonical CBOR encoding for signed fields.

## Registration

```csharp
services.AddSunfishKernelSecurity();

// Callers must also register an IKeystore:
services.AddSingleton<IKeystore>(_ => Keystore.CreateForCurrentPlatform());
```

## Related

- Paper §11.2 Layer 2 — Field-level encryption (consumers of role keys).
- Paper §10.2 — Bucket eligibility evaluated at capability-negotiation from attestations.
- Paper §13.4 — QR-code onboarding ships an initial attestation bundle.
- Wave 1.4 — `foundation-localfirst/Encryption/IKeystore` (role keys are stored through it).
- Wave 2.4 — `packages/kernel-buckets` (consumes attestations at bucket-eligibility time).
- Wave 3.4 — QR-code onboarding (ships an `AttestationBundle`).
