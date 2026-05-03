using System.Buffers.Binary;
using System.Formats.Cbor;
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Foundation.LocalFirst.Encryption;
using Sunfish.Kernel.Runtime.Teams;
using Sunfish.Kernel.Security.Attestation;
using Sunfish.Kernel.Security.Crypto;
using Sunfish.Kernel.Security.Keys;
using Sunfish.Kernel.Sync.Identity;

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

    /// <summary>
    /// Per-team invitation payload schema tag. The byte string is literal
    /// "sunfish-team-invite-v1" — bumped when the wire fields change.
    /// </summary>
    private const string InvitationSchemaTag = "sunfish-team-invite-v1";

    /// <summary>Lifetime of an invitation. Callers that don't scan within the
    /// window must request a fresh invitation — we don't renew silently.</summary>
    private static readonly TimeSpan InvitationValidity = TimeSpan.FromMinutes(10);

    private readonly IEd25519Signer _signer;
    private readonly IActiveTeamAccessor _activeTeam;
    private readonly IAttestationVerifier _verifier;
    private readonly IAttestationIssuer _issuer;
    private readonly TimeProvider _clock;

    // Wave 6.8 deps for join-additional-team. All four are nullable so existing
    // unit tests (which exercise only the Wave 3.4 encode/decode surface)
    // continue to compile against the old 4-arg ctor. The new Part-2 methods
    // (Begin/CompleteAdditionalTeamJoinAsync) assert non-null at call time.
    private readonly ITeamContextFactory? _factory;
    private readonly ITeamStoreActivator? _storeActivator;
    private readonly ITeamSubkeyDerivation? _subkeyDerivation;
    private readonly NodeIdentity? _rootIdentity;

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
    /// Wave 6.8 overload: extends the ctor with the multi-team surfaces needed
    /// for the join-additional-team flow. Existing tests + call-sites that only
    /// use encode/decode/founder/joiner methods can keep using the 4-arg ctor;
    /// the <c>MauiProgram</c> composition root resolves the 8-arg ctor so the
    /// Anchor shell can begin/complete additional-team joins.
    /// </summary>
    /// <param name="signer">Ed25519 signer.</param>
    /// <param name="activeTeam">Active-team accessor.</param>
    /// <param name="verifier">Attestation verifier.</param>
    /// <param name="issuer">Attestation issuer.</param>
    /// <param name="factory">Team context factory — used to materialize the
    /// new team on begin.</param>
    /// <param name="storeActivator">Per-team encrypted-store activator — opens
    /// the SQLCipher DB for the new team.</param>
    /// <param name="subkeyDerivation">Per-team subkey derivation (ADR 0032 §Device identity).</param>
    /// <param name="rootIdentity">The install's root Ed25519 identity.</param>
    /// <param name="clock">Time source. Defaults to <see cref="TimeProvider.System"/>.</param>
    public QrOnboardingService(
        IEd25519Signer signer,
        IActiveTeamAccessor activeTeam,
        IAttestationVerifier verifier,
        IAttestationIssuer issuer,
        ITeamContextFactory factory,
        ITeamStoreActivator storeActivator,
        ITeamSubkeyDerivation subkeyDerivation,
        NodeIdentity rootIdentity,
        TimeProvider? clock = null)
        : this(signer, activeTeam, verifier, issuer, clock)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _storeActivator = storeActivator ?? throw new ArgumentNullException(nameof(storeActivator));
        _subkeyDerivation = subkeyDerivation ?? throw new ArgumentNullException(nameof(subkeyDerivation));
        _rootIdentity = rootIdentity ?? throw new ArgumentNullException(nameof(rootIdentity));
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

    /// <summary>
    /// Wave 6.8 — begin a "join an additional team" flow. Creates a fresh
    /// <see cref="TeamContext"/> with a freshly-derived per-team subkey (via
    /// <see cref="ITeamSubkeyDerivation"/>), does NOT touch existing teams.
    /// Returns the new <see cref="TeamId"/> + a QR payload the user scans on the
    /// team's founder device to request membership.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The returned <see cref="AdditionalTeamJoinInvitation.QrPayload"/> is a
    /// CBOR-encoded tuple:
    /// <code>
    /// ["sunfish-team-invite-v1",
    ///  team_id (16B GUID big-endian),
    ///  install_per_team_public_key (32B Ed25519),
    ///  nonce (16B),
    ///  issued_at (unix-seconds int),
    ///  expires_at (unix-seconds int),
    ///  signature (64B Ed25519 over the concatenation of the preceding fields,
    ///            signed with the install's <em>root</em> Ed25519 private key)]
    /// </code>
    /// The founder device verifies the signature against the install's root
    /// public key (acquired out-of-band — typically by scanning the joiner's
    /// root public-key QR during initial setup; for v0 the "expected root
    /// public key" is implicit in whatever trust anchor the founder device
    /// already holds).
    /// </para>
    /// <para>
    /// Begin does NOT call <see cref="IActiveTeamAccessor.SetActiveAsync"/> —
    /// auto-switching would disorient the user (they may still be completing
    /// work in the current team). The switcher UI drives active-team changes.
    /// </para>
    /// </remarks>
    public async ValueTask<AdditionalTeamJoinInvitation> BeginAdditionalTeamJoinAsync(
        CancellationToken ct)
    {
        EnsureWave68Wired();
        ct.ThrowIfCancellationRequested();

        // 1. Fresh TeamId — a pure Guid.NewGuid, no correlation with existing
        //    teams or with any value the founder device might already know.
        var teamId = TeamId.New();

        // 2. Derive the per-team subkey. The subkey's public key is the value
        //    we transmit in the invitation — the founder signs a response
        //    bundle keyed off it.
        var (teamPub, _) = _subkeyDerivation!.DeriveTeamKeypair(
            _rootIdentity!.PrivateKey, teamId.Value.ToString("D"));

        // 3. Materialize the team in DI. Display name is provisional — the
        //    founder's CompleteAdditionalTeamJoin response may carry a
        //    canonical name; until then the GUID suffix is visible in the
        //    switcher. Users can rename post-join (future wave).
        var displayName = $"Team {teamId.Value:D}";
        await _factory!.GetOrCreateAsync(teamId, displayName, ct).ConfigureAwait(false);
        await _storeActivator!.ActivateAsync(teamId, ct).ConfigureAwait(false);

        // 4. Build + sign the invitation CBOR. Nonce is 16 random bytes —
        //    prevents QR-replay across sessions + makes the signature unique
        //    even if the same team-id + timestamp are ever re-issued.
        var nonce = new byte[16];
        System.Security.Cryptography.RandomNumberGenerator.Fill(nonce);
        var issuedAt = _clock.GetUtcNow();
        var expiresAt = issuedAt + InvitationValidity;

        // Canonical signable = schema-tag-bytes || team-id-bytes || pub || nonce
        //   || issuedAt (8B BE unix-seconds) || expiresAt (8B BE unix-seconds).
        // The signature lives in the CBOR envelope as the last array element;
        // we sign the raw concatenation so the wire form and the signable form
        // don't have to share a canonical-CBOR pass.
        var teamIdBytes = teamId.Value.ToByteArray();
        var signable = BuildInvitationSignable(
            teamIdBytes, teamPub, nonce, issuedAt, expiresAt);
        var signature = _signer.Sign(signable, _rootIdentity.PrivateKey);

        var payload = EncodeInvitationCbor(
            teamIdBytes, teamPub, nonce, issuedAt, expiresAt, signature);

        return new AdditionalTeamJoinInvitation(
            TeamId: teamId,
            QrPayload: payload,
            ExpiresAt: expiresAt);
    }

    /// <summary>
    /// Wave 6.8 — finalize an additional-team join using the signed response
    /// bundle produced by the founder device. The bundle is expected to carry
    /// a <c>team_member</c> <see cref="RoleAttestation"/> issued by the
    /// founder. This method verifies the bundle, persists the attestation into
    /// the new team's encrypted store, and raises no active-team switch —
    /// the caller decides when (and whether) to make the new team active via
    /// <see cref="IActiveTeamAccessor.SetActiveAsync"/>.
    /// </summary>
    /// <param name="teamId">Team id returned by
    /// <see cref="BeginAdditionalTeamJoinAsync"/>. Must already be
    /// materialized — otherwise the caller is holding a stale invitation.</param>
    /// <param name="signedBundle">A CBOR-encoded <see cref="AttestationBundle"/>
    /// containing at least one <see cref="RoleAttestation"/> whose
    /// <see cref="RoleAttestation.TeamId"/> matches <paramref name="teamId"/>
    /// and whose signature verifies against the attestation's own
    /// <see cref="RoleAttestation.IssuerPublicKey"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="ArgumentException">The bundle is not parseable CBOR or
    /// the team id mismatches.</exception>
    /// <exception cref="InvalidOnboardingPayloadException">The bundle fails
    /// attestation verification (expired, tampered, issuer-mismatched).</exception>
    public async ValueTask CompleteAdditionalTeamJoinAsync(
        TeamId teamId,
        byte[] signedBundle,
        CancellationToken ct)
    {
        EnsureWave68Wired();
        ArgumentNullException.ThrowIfNull(signedBundle);
        ct.ThrowIfCancellationRequested();

        // 1. Parse + verify the response bundle.
        AttestationBundle bundle;
        try
        {
            bundle = AttestationBundle.FromCbor(signedBundle);
        }
        catch (Exception ex)
        {
            throw new ArgumentException(
                "Additional-team response bundle is not valid CBOR.",
                nameof(signedBundle), ex);
        }

        if (bundle.Attestations.Count == 0)
        {
            throw new InvalidOnboardingPayloadException(
                "Additional-team response bundle contains no attestations.");
        }

        var teamIdBytes = teamId.Value.ToByteArray();
        var now = _clock.GetUtcNow();
        foreach (var att in bundle.Attestations)
        {
            if (!att.TeamId.AsSpan().SequenceEqual(teamIdBytes))
            {
                throw new InvalidOnboardingPayloadException(
                    $"Attestation team id does not match the invitation's team id {teamId.Value:D}.");
            }
            if (!_verifier.Verify(att, att.IssuerPublicKey, now))
            {
                throw new InvalidOnboardingPayloadException(
                    $"Additional-team attestation for role '{att.Role}' failed verification (expired, tampered, or issuer-mismatched).");
            }
        }

        // 2. Resolve the team's encrypted store + persist the attestation.
        var context = FindTeamContext(teamId)
            ?? throw new InvalidOperationException(
                $"Team {teamId.Value:D} was not materialized — call BeginAdditionalTeamJoinAsync first.");
        var store = context.Services.GetRequiredService<IEncryptedStore>();

        // Key layout matches the Wave 3.4 founder path: the accepted bundle is
        // written to a stable well-known key so later launches can re-hydrate
        // the team's role state without replaying the join.
        await store.SetAsync(
            "onboarding.attestation-bundle",
            bundle.ToCbor(),
            ct).ConfigureAwait(false);

        // Carve-out noted in the wave spec: we do NOT call
        // _activeTeam.SetActiveAsync here — the switcher UI drives that.
    }

    /// <summary>
    /// Builds the canonical signable byte sequence for an invitation. Kept
    /// internal so tests can re-sign tampered inputs to exercise
    /// signature-verification branches.
    /// </summary>
    private static byte[] BuildInvitationSignable(
        byte[] teamIdBytes,
        byte[] teamPublicKey,
        byte[] nonce,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt)
    {
        var schemaTagBytes = System.Text.Encoding.UTF8.GetBytes(InvitationSchemaTag);
        var buffer = new byte[
            schemaTagBytes.Length
            + teamIdBytes.Length
            + teamPublicKey.Length
            + nonce.Length
            + 8
            + 8];
        var offset = 0;
        Buffer.BlockCopy(schemaTagBytes, 0, buffer, offset, schemaTagBytes.Length);
        offset += schemaTagBytes.Length;
        Buffer.BlockCopy(teamIdBytes, 0, buffer, offset, teamIdBytes.Length);
        offset += teamIdBytes.Length;
        Buffer.BlockCopy(teamPublicKey, 0, buffer, offset, teamPublicKey.Length);
        offset += teamPublicKey.Length;
        Buffer.BlockCopy(nonce, 0, buffer, offset, nonce.Length);
        offset += nonce.Length;
        BinaryPrimitives.WriteInt64BigEndian(
            buffer.AsSpan(offset, 8), issuedAt.ToUnixTimeSeconds());
        offset += 8;
        BinaryPrimitives.WriteInt64BigEndian(
            buffer.AsSpan(offset, 8), expiresAt.ToUnixTimeSeconds());
        return buffer;
    }

    private static byte[] EncodeInvitationCbor(
        byte[] teamIdBytes,
        byte[] teamPublicKey,
        byte[] nonce,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        byte[] signature)
    {
        var writer = new CborWriter(CborConformanceMode.Canonical, convertIndefiniteLengthEncodings: true);
        writer.WriteStartArray(7);
        writer.WriteTextString(InvitationSchemaTag);
        writer.WriteByteString(teamIdBytes);
        writer.WriteByteString(teamPublicKey);
        writer.WriteByteString(nonce);
        writer.WriteInt64(issuedAt.ToUnixTimeSeconds());
        writer.WriteInt64(expiresAt.ToUnixTimeSeconds());
        writer.WriteByteString(signature);
        writer.WriteEndArray();
        return writer.Encode();
    }

    private TeamContext? FindTeamContext(TeamId teamId)
    {
        if (_factory is null)
        {
            return null;
        }
        foreach (var ctx in _factory.Active)
        {
            if (ctx.TeamId.Equals(teamId))
            {
                return ctx;
            }
        }
        return null;
    }

    private void EnsureWave68Wired()
    {
        if (_factory is null || _storeActivator is null
            || _subkeyDerivation is null || _rootIdentity is null)
        {
            throw new InvalidOperationException(
                "QrOnboardingService was constructed without the Wave 6.8 multi-team dependencies. " +
                "Use the 8-arg ctor (factory, storeActivator, subkeyDerivation, rootIdentity) " +
                "to call BeginAdditionalTeamJoinAsync / CompleteAdditionalTeamJoinAsync.");
        }
    }
}

/// <summary>
/// Wave 6.8 — invitation payload returned from
/// <see cref="QrOnboardingService.BeginAdditionalTeamJoinAsync"/>. The Anchor
/// UI renders <see cref="QrPayload"/> as a QR code (or pastes it as base64) so
/// the founder device can sign a response attestation bundle.
/// </summary>
/// <param name="TeamId">Freshly-minted team id; becomes the identity of the
/// new <see cref="TeamContext"/>.</param>
/// <param name="QrPayload">CBOR-encoded invitation (schema-tagged, signed with
/// the install's root Ed25519 private key).</param>
/// <param name="ExpiresAt">Wall-clock time after which the invitation should
/// be rejected. The UI may start a countdown + offer a regenerate button.</param>
public sealed record AdditionalTeamJoinInvitation(
    TeamId TeamId,
    byte[] QrPayload,
    DateTimeOffset ExpiresAt);

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
