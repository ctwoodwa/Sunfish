namespace Sunfish.Bridge.Orchestration;

/// <summary>
/// Derives a per-tenant 32-byte Ed25519 seed from Bridge's install-level
/// <see cref="Sunfish.Kernel.Security.Keys.IRootSeedProvider"/> root seed.
/// Wave 5.2 stop-work #1: without this contract, every tenant child spawned
/// by <see cref="TenantProcessSupervisor"/> reads the same install-level seed
/// from the keystore, yielding identical Ed25519 + SQLCipher keys across
/// tenants on one Bridge host. This provider HKDF-expands the root seed with
/// a per-tenant <c>info</c> label so that N tenants on one Bridge install
/// derive N cryptographically independent root identities.
/// </summary>
/// <remarks>
/// <para>
/// <b>Derivation.</b> The tenant seed is
/// <c>HKDF-Expand(SHA-256, rootSeed, info, 32)</c> where
/// <c>info = "sunfish:bridge:tenant-seed:v1:" + tenantId.ToString("D")</c>.
/// No salt is used (HKDF-Expand with a uniformly-random PRK). The <c>v1</c>
/// version tag is part of the info label so future rotations (e.g., moving to
/// HKDF-SHA-512 or a different info shape) can coexist with v1 seeds during
/// migration.
/// </para>
/// <para>
/// <b>Properties.</b> Deterministic — same <see cref="Guid"/> always yields
/// the same seed on the same Bridge install. Cryptographically independent
/// across tenants (distinct info labels). Cryptographically independent from
/// the Bridge root seed (HKDF-Expand output reveals nothing about its PRK).
/// </para>
/// <para>
/// <b>Trust model.</b> Bridge (the parent supervisor process) passes the
/// derived seed to each spawned <c>local-node-host</c> child via an
/// environment variable; the child trusts the injected seed and skips its own
/// keystore lookup. This is safe because parent and children share the same
/// security domain — deeper attestation (signed seed envelopes, per-child
/// keystore slots) is a future wave.
/// </para>
/// </remarks>
public interface ITenantSeedProvider
{
    /// <summary>
    /// Derives the 32-byte Ed25519 seed for the given tenant via HKDF-SHA256
    /// from the Bridge install-level root seed. Deterministic: same
    /// <paramref name="tenantId"/> always yields the same seed on the same
    /// Bridge install. Cryptographically independent across tenants and from
    /// the Bridge root seed itself.
    /// </summary>
    /// <param name="tenantId">The tenant identifier. <see cref="Guid.Empty"/>
    /// is permitted (no special meaning) — the caller is responsible for
    /// validating that the id refers to a real tenant registration.</param>
    /// <returns>A freshly-allocated 32-byte buffer. Callers must treat the
    /// buffer as secret-material and must not persist it outside the tenant
    /// child process.</returns>
    byte[] DeriveTenantSeed(Guid tenantId);
}
