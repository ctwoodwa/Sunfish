using System.Buffers.Binary;
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Foundation.LocalFirst.Encryption;
using Sunfish.Kernel.Runtime.Teams;
using Sunfish.Kernel.Security.Attestation;
using Sunfish.Kernel.Security.Crypto;

namespace Sunfish.Anchor.Services;

/// <summary>
/// QR-code onboarding payload encoder / decoder per paper §13.4.
/// </summary>
/// <remarks>
/// <para>
/// Payload format (little-endian lengths, CBOR bundle, raw snapshot):
/// </para>
/// <code>
/// [4 bytes: CBOR bundle length]
/// [N bytes: CBOR-encoded AttestationBundle]
/// [4 bytes: snapshot length]
/// [M bytes: raw snapshot bytes]
/// </code>
/// <para>
/// This wave ships the bytes-level roundtrip. The camera/QR-decode path is a
/// TODO (deferred platform integration); an encode-then-base64 path is the
/// reference transport for tests and for the paste-bundle fallback UI.
/// </para>
/// </remarks>
public sealed class QrOnboardingService
{
    private const string TeamMemberRole = "team_member";
    private const string AdminRole = "admin";
    private static readonly TimeSpan DefaultAttestationValidity = TimeSpan.FromDays(365);

    private readonly IEd25519Signer _signer;
    private readonly IActiveTeamAccessor _activeTeam;
    private readonly IAttestationVerifier _verifier;
    private readonly IAttestationIssuer _issuer;
    private readonly TimeProvider _clock;

    /// <summary>
    /// Creates a new onboarding service. Wave 6.3.F: the
    /// <see cref="IEncryptedStore"/> is resolved on demand through the active
    /// <see cref="TeamContext"/> rather than taken as a direct ctor dep; the
    /// Anchor shell binds its encrypted store per-team per ADR 0032.
    /// </summary>
    public QrOnboardingService(
        IEd25519Signer signer,
        IActiveTeamAccessor activeTeam,
        IAttestationVerifier verifier,
        IAttestationIssuer issuer,
        TimeProvider? clock = null)
    {
        _signer = signer ?? throw new ArgumentNullException(nameof(signer));
        _activeTeam = activeTeam ?? throw new ArgumentNullException(nameof(activeTeam));
        _verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));
        _issuer = issuer ?? throw new ArgumentNullException(nameof(issuer));
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>
    /// Decodes a QR payload (or pasted base64 bundle) into its constituent
    /// <see cref="AttestationBundle"/> and initial snapshot.
    /// </summary>
    /// <exception cref="ArgumentException">The payload is malformed or truncated.</exception>
    /// <exception cref="InvalidOnboardingPayloadException">
    /// The payload is syntactically valid but contains an expired or
    /// signature-invalid attestation.
    /// </exception>
    public Task<OnboardingResult> DecodePayloadAsync(
        ReadOnlyMemory<byte> qrPayload,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var span = qrPayload.Span;
        if (span.Length < 8)
        {
            throw new ArgumentException(
                "Onboarding payload must be at least 8 bytes (two length prefixes).",
                nameof(qrPayload));
        }

        var bundleLen = BinaryPrimitives.ReadUInt32LittleEndian(span[..4]);
        if (span.Length < 4 + bundleLen + 4)
        {
            throw new ArgumentException(
                $"Onboarding payload truncated: expected >= {4 + bundleLen + 4} bytes for bundle+len prefix, got {span.Length}.",
                nameof(qrPayload));
        }

        var bundleBytes = span.Slice(4, (int)bundleLen);
        AttestationBundle bundle;
        try
        {
            bundle = AttestationBundle.FromCbor(bundleBytes);
        }
        catch (Exception ex)
        {
            throw new ArgumentException("Onboarding payload contains a malformed AttestationBundle.", nameof(qrPayload), ex);
        }

        var snapOffset = 4 + (int)bundleLen;
        var snapLen = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(snapOffset, 4));
        if (span.Length < snapOffset + 4 + snapLen)
        {
            throw new ArgumentException(
                $"Onboarding payload truncated: expected >= {snapOffset + 4 + snapLen} bytes for snapshot, got {span.Length}.",
                nameof(qrPayload));
        }

        var snapshot = span.Slice(snapOffset + 4, (int)snapLen).ToArray();

        if (bundle.Attestations.Count == 0)
        {
            throw new InvalidOnboardingPayloadException("AttestationBundle contains no attestations.");
        }

        // Verify each attestation against its own declared issuer public key.
        // Founder bundles are self-signed (issuer == subject); joiner bundles
        // are signed by the admin. Either way, the issuer key lives in the
        // attestation itself — we don't carry a separate trust anchor on the wire.
        var now = _clock.GetUtcNow();
        foreach (var att in bundle.Attestations)
        {
            if (!_verifier.Verify(att, att.IssuerPublicKey, now))
            {
                throw new InvalidOnboardingPayloadException(
                    $"AttestationBundle attestation for role '{att.Role}' failed verification (expired, tampered, or issuer-mismatched).");
            }
        }

        // For the joining-node case we need to materialize a device keypair
        // locally. For the founder case the issuer *is* the device and we
        // don't have its private key on the receiving side — so we return
        // empty private-key bytes and expect callers to use the founder
        // flow (GenerateFounderBundleAsync) instead of decoding their own
        // just-emitted bundle.
        var (devicePub, devicePriv) = _signer.GenerateKeyPair();
        return Task.FromResult(new OnboardingResult(
            Bundle: bundle,
            InitialSnapshot: snapshot,
            DevicePrivateKey: devicePriv,
            DevicePublicKey: devicePub));
    }

    /// <summary>
    /// Generates a new team's founder bundle: fresh Ed25519 keypair, a
    /// 16-byte random team id, and a self-signed <c>admin</c> attestation.
    /// </summary>
    public Task<OnboardingResult> GenerateFounderBundleAsync(
        string teamName,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(teamName);
        ct.ThrowIfCancellationRequested();

        var (devicePub, devicePriv) = _signer.GenerateKeyPair();
        var teamId = new byte[RoleAttestation.TeamIdLength];
        System.Security.Cryptography.RandomNumberGenerator.Fill(teamId);

        // Self-attestation: issuer == subject. This is the founder-node pattern
        // the paper §11.3 footnote admits as the bootstrap case — a single
        // admin is the trust root until a second device is onboarded.
        var attestation = _issuer.Issue(
            teamId: teamId,
            subjectPublicKey: devicePub,
            role: AdminRole,
            validity: DefaultAttestationValidity,
            issuerPrivateKey: devicePriv);

        var bundle = new AttestationBundle(new[] { attestation });

        // Empty snapshot — the founder has no prior state to seed.
        return Task.FromResult(new OnboardingResult(
            Bundle: bundle,
            InitialSnapshot: ReadOnlyMemory<byte>.Empty,
            DevicePrivateKey: devicePriv,
            DevicePublicKey: devicePub));
    }

    /// <summary>
    /// Encodes an <see cref="AttestationBundle"/> + snapshot into the QR-ready
    /// wire format (see remarks on the class). Callers can base64-encode the
    /// result for the paste-bundle fallback UI.
    /// </summary>
    public Task<ReadOnlyMemory<byte>> EncodePayloadAsync(
        AttestationBundle bundle,
        ReadOnlyMemory<byte> snapshot,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ct.ThrowIfCancellationRequested();

        var bundleBytes = bundle.ToCbor();
        var snapLen = snapshot.Length;
        var buffer = new byte[4 + bundleBytes.Length + 4 + snapLen];

        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0, 4), (uint)bundleBytes.Length);
        bundleBytes.CopyTo(buffer, 4);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(4 + bundleBytes.Length, 4), (uint)snapLen);
        if (snapLen > 0)
        {
            snapshot.Span.CopyTo(buffer.AsSpan(4 + bundleBytes.Length + 4));
        }

        return Task.FromResult<ReadOnlyMemory<byte>>(buffer);
    }

    /// <summary>
    /// Issues a <c>team_member</c> attestation signed by the founder's
    /// private key. Useful when the founder device wants to encode a QR for a
    /// joining device; the founder bundles the joiner's public key into a
    /// signed record so the joiner's <see cref="DecodePayloadAsync"/> call
    /// accepts the bundle.
    /// </summary>
    public Task<AttestationBundle> IssueJoinerAttestationAsync(
        byte[] teamId,
        byte[] joinerPublicKey,
        ReadOnlyMemory<byte> founderPrivateKey,
        TimeSpan? validity = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(teamId);
        ArgumentNullException.ThrowIfNull(joinerPublicKey);
        ct.ThrowIfCancellationRequested();

        var attestation = _issuer.Issue(
            teamId: teamId,
            subjectPublicKey: joinerPublicKey,
            role: TeamMemberRole,
            validity: validity ?? DefaultAttestationValidity,
            issuerPrivateKey: founderPrivateKey);

        return Task.FromResult(new AttestationBundle(new[] { attestation }));
    }

    /// <summary>
    /// Resolve the active team's <see cref="IEncryptedStore"/>. Wave 6.3.F:
    /// the service no longer owns a singleton store reference; instead it
    /// pulls the store from the active <see cref="TeamContext"/>'s services on
    /// every access so team-switcher flows (Wave 6.6) see the right team's
    /// data immediately after
    /// <see cref="IActiveTeamAccessor.SetActiveAsync"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">No team is currently
    /// active — the caller must drive
    /// <see cref="IActiveTeamAccessor.SetActiveAsync"/> (normally via
    /// <see cref="AnchorBootstrapHostedService"/> at app launch) before any
    /// store-touching path runs.</exception>
    private IEncryptedStore Store =>
        _activeTeam.Active?.Services.GetRequiredService<IEncryptedStore>()
        ?? throw new InvalidOperationException(
            "No active team — call IActiveTeamAccessor.SetActiveAsync first.");
}

/// <summary>
/// Onboarding decode/generate result. <see cref="DevicePrivateKey"/> must be
/// written to the OS keystore by the caller; returning raw bytes from the
/// service keeps test wiring simple.
/// </summary>
public sealed record OnboardingResult(
    AttestationBundle Bundle,
    ReadOnlyMemory<byte> InitialSnapshot,
    byte[] DevicePrivateKey,
    byte[] DevicePublicKey);

/// <summary>
/// Thrown when an onboarding payload is syntactically well-formed but fails
/// attestation verification (expired, tampered, or issuer-mismatched).
/// </summary>
public sealed class InvalidOnboardingPayloadException : Exception
{
    /// <summary>Initializes with a default message.</summary>
    public InvalidOnboardingPayloadException()
        : base("The onboarding payload is invalid.") { }

    /// <summary>Initializes with a custom message.</summary>
    public InvalidOnboardingPayloadException(string message) : base(message) { }

    /// <summary>Initializes with a custom message and inner exception.</summary>
    public InvalidOnboardingPayloadException(string message, Exception innerException)
        : base(message, innerException) { }
}
