using System.Security.Cryptography;
using System.Text;
using Sunfish.Kernel.Security.Crypto;

namespace Sunfish.Kernel.Security.Recovery;

/// <summary>
/// Production implementation of <see cref="IRecoveryCoordinator"/> per
/// ADR 0046. Pure orchestration — all I/O is delegated to
/// <see cref="IRecoveryStateStore"/>; all time-of-day reads go through
/// <see cref="IRecoveryClock"/>; signature operations via
/// <see cref="IEd25519Signer"/>; dispute authorization via
/// <see cref="IDisputerValidator"/>.
/// </summary>
public sealed class RecoveryCoordinator : IRecoveryCoordinator
{
    private const string EventHashDomainPrefix = "sunfish-recovery-event-v1\n";
    private const byte FieldSeparator = 0x1E; // ASCII record separator

    private readonly IRecoveryClock _clock;
    private readonly IRecoveryStateStore _store;
    private readonly IEd25519Signer _signer;
    private readonly IDisputerValidator _disputerValidator;
    private readonly RecoveryCoordinatorOptions _options;
    private readonly SemaphoreSlim _gate = new(initialCount: 1, maxCount: 1);

    /// <summary>
    /// Construct a coordinator. Inject the persistence store, the clock,
    /// the Ed25519 signer (for signature verification), and the disputer
    /// validator. <paramref name="options"/> defaults to ADR 0046's
    /// 3-of-5 quorum + 7-day grace if omitted.
    /// </summary>
    public RecoveryCoordinator(
        IRecoveryClock clock,
        IRecoveryStateStore store,
        IEd25519Signer signer,
        IDisputerValidator disputerValidator,
        RecoveryCoordinatorOptions? options = null)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _signer = signer ?? throw new ArgumentNullException(nameof(signer));
        _disputerValidator = disputerValidator ?? throw new ArgumentNullException(nameof(disputerValidator));
        _options = options ?? new RecoveryCoordinatorOptions();
        ValidateOptions(_options);
    }

    /// <inheritdoc />
    public async Task<RecoveryEvent> DesignateTrusteeAsync(
        string trusteeNodeId,
        ReadOnlyMemory<byte> trusteePublicKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(trusteeNodeId);
        if (trusteePublicKey.Length != RecoveryRequest.EphemeralPublicKeyLength)
        {
            throw new ArgumentException(
                $"Trustee public key must be {RecoveryRequest.EphemeralPublicKeyLength} bytes; got {trusteePublicKey.Length}.",
                nameof(trusteePublicKey));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var state = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
            if (state.Trustees.ContainsKey(trusteeNodeId))
            {
                throw new InvalidOperationException(
                    $"Trustee '{trusteeNodeId}' is already designated.");
            }
            if (state.Trustees.Count >= _options.MaxTrustees)
            {
                throw new InvalidOperationException(
                    $"Trustee set is at capacity ({_options.MaxTrustees}); revoke an existing trustee before designating a new one.");
            }

            var now = _clock.UtcNow();
            var designation = new TrusteeDesignation(
                trusteeNodeId,
                trusteePublicKey.ToArray(),
                now);
            var trustees = new Dictionary<string, TrusteeDesignation>(state.Trustees, StringComparer.Ordinal)
            {
                [trusteeNodeId] = designation,
            };

            var (evt, hashAfter) = AppendEvent(
                state,
                RecoveryEventType.TrusteeDesignated,
                actorNodeId: trusteeNodeId,
                targetNodeId: trusteeNodeId,
                occurredAt: now,
                detail: BuildDetail(("trustee.publicKey.hex", Convert.ToHexString(trusteePublicKey.Span))));

            var next = CloneStateWith(state, trustees: trustees, lastEventHash: hashAfter);
            await _store.SaveAsync(next, cancellationToken).ConfigureAwait(false);
            return evt;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<RecoveryEvent> RevokeTrusteeAsync(
        string trusteeNodeId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(trusteeNodeId);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var state = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
            if (!state.Trustees.ContainsKey(trusteeNodeId))
            {
                throw new InvalidOperationException(
                    $"Trustee '{trusteeNodeId}' is not currently designated.");
            }

            var trustees = new Dictionary<string, TrusteeDesignation>(state.Trustees, StringComparer.Ordinal);
            trustees.Remove(trusteeNodeId);

            var now = _clock.UtcNow();
            var (evt, hashAfter) = AppendEvent(
                state,
                RecoveryEventType.TrusteeRevoked,
                actorNodeId: trusteeNodeId,
                targetNodeId: trusteeNodeId,
                occurredAt: now,
                detail: BuildDetail());

            var next = CloneStateWith(state, trustees: trustees, lastEventHash: hashAfter);
            await _store.SaveAsync(next, cancellationToken).ConfigureAwait(false);
            return evt;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<RecoveryEvent> InitiateRecoveryAsync(
        RecoveryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.VerifySignature(_signer))
        {
            throw new ArgumentException("Recovery request signature is invalid.", nameof(request));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var state = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
            if (state.PendingRequest is not null && !state.Disputed && !state.Completed)
            {
                throw new InvalidOperationException(
                    "A prior recovery request is still in flight. Resolve it (dispute or complete) before initiating another.");
            }

            var now = _clock.UtcNow();
            var resetState = new RecoveryCoordinatorState
            {
                Trustees = state.Trustees,
                PendingRequest = request,
                Attestations = new Dictionary<string, TrusteeAttestation>(StringComparer.Ordinal),
                GracePeriodStartedAt = null,
                Disputed = false,
                Completed = false,
                LastEventHash = state.LastEventHash,
            };

            var (evt, hashAfter) = AppendEvent(
                resetState,
                RecoveryEventType.RecoveryInitiated,
                actorNodeId: request.RequestingNodeId,
                targetNodeId: request.RequestingNodeId,
                occurredAt: now,
                detail: BuildDetail(
                    ("request.requestedAt", request.RequestedAt.ToString("O")),
                    ("request.ephemeralPublicKey.hex", Convert.ToHexString(request.EphemeralPublicKey))));

            var next = CloneStateWith(resetState, lastEventHash: hashAfter);
            await _store.SaveAsync(next, cancellationToken).ConfigureAwait(false);
            return evt;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<RecoveryAttestationOutcome> SubmitAttestationAsync(
        TrusteeAttestation attestation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attestation);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var state = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);

            // Drop conditions (silent: per ADR 0046 the social-recovery flow tolerates
            // garbage-in without exception so trustees can fan-out broadcast cheaply).
            if (state.PendingRequest is null) return Drop();
            if (state.Disputed || state.Completed) return Drop();
            if (!state.Trustees.ContainsKey(attestation.TrusteeNodeId)) return Drop();
            if (state.Attestations.ContainsKey(attestation.TrusteeNodeId)) return Drop();
            if (!attestation.Verify(state.PendingRequest, _signer)) return Drop();

            // Trustee public-key fingerprint must match the designated record so a
            // trustee key-rotation between designation and attestation does not silently
            // pass through. Constant-time comparison.
            var designated = state.Trustees[attestation.TrusteeNodeId];
            if (designated.PublicKey.Length != attestation.TrusteePublicKey.Length
                || !CryptographicOperations.FixedTimeEquals(designated.PublicKey, attestation.TrusteePublicKey))
            {
                return Drop();
            }

            var now = _clock.UtcNow();
            var attestations = new Dictionary<string, TrusteeAttestation>(state.Attestations, StringComparer.Ordinal)
            {
                [attestation.TrusteeNodeId] = attestation,
            };

            // First event: AttestationReceived.
            var (received, hashAfterReceived) = AppendEvent(
                state,
                RecoveryEventType.AttestationReceived,
                actorNodeId: attestation.TrusteeNodeId,
                targetNodeId: state.PendingRequest.RequestingNodeId,
                occurredAt: now,
                detail: BuildDetail(
                    ("attestation.attestedAt", attestation.AttestedAt.ToString("O")),
                    ("attestation.signature.hex", Convert.ToHexString(attestation.Signature)),
                    ("quorum.received", attestations.Count.ToString()),
                    ("quorum.threshold", _options.QuorumThreshold.ToString())));

            var events = new List<RecoveryEvent> { received };
            var lastHash = hashAfterReceived;
            DateTimeOffset? gracePeriodStartedAt = state.GracePeriodStartedAt;

            // Quorum check — on the new attestation set, count entries whose trustee
            // is *currently* designated. Revoked trustees' stale attestations don't
            // count toward quorum, but a revocation after grace started doesn't roll
            // back the timer.
            if (gracePeriodStartedAt is null)
            {
                var activeAttestations = 0;
                foreach (var nodeId in attestations.Keys)
                {
                    if (state.Trustees.ContainsKey(nodeId)) activeAttestations++;
                }

                if (activeAttestations >= _options.QuorumThreshold)
                {
                    gracePeriodStartedAt = now;
                    var graceEndAt = now + _options.GracePeriod;
                    var stateAfterReceived = new RecoveryCoordinatorState
                    {
                        Trustees = state.Trustees,
                        PendingRequest = state.PendingRequest,
                        Attestations = attestations,
                        GracePeriodStartedAt = gracePeriodStartedAt,
                        Disputed = state.Disputed,
                        Completed = state.Completed,
                        LastEventHash = lastHash,
                    };

                    var (started, hashAfterStarted) = AppendEvent(
                        stateAfterReceived,
                        RecoveryEventType.GracePeriodStarted,
                        actorNodeId: state.PendingRequest.RequestingNodeId,
                        targetNodeId: state.PendingRequest.RequestingNodeId,
                        occurredAt: now,
                        detail: BuildDetail(
                            ("grace.elapsesAt", graceEndAt.ToString("O")),
                            ("grace.duration", _options.GracePeriod.ToString())));
                    events.Add(started);
                    lastHash = hashAfterStarted;
                }
            }

            var next = new RecoveryCoordinatorState
            {
                Trustees = state.Trustees,
                PendingRequest = state.PendingRequest,
                Attestations = attestations,
                GracePeriodStartedAt = gracePeriodStartedAt,
                Disputed = state.Disputed,
                Completed = state.Completed,
                LastEventHash = lastHash,
            };
            await _store.SaveAsync(next, cancellationToken).ConfigureAwait(false);
            return new RecoveryAttestationOutcome(Accepted: true, Events: events);

            static RecoveryAttestationOutcome Drop() => new(Accepted: false, Events: Array.Empty<RecoveryEvent>());
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<RecoveryEvent> DisputeRecoveryAsync(
        RecoveryDispute dispute,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dispute);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var state = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
            if (state.PendingRequest is null)
            {
                throw new InvalidOperationException("No recovery request is in flight.");
            }
            if (state.Completed)
            {
                throw new InvalidOperationException("Recovery already completed; dispute window has closed.");
            }
            if (state.Disputed)
            {
                throw new InvalidOperationException("Recovery already disputed.");
            }
            if (state.GracePeriodStartedAt is null)
            {
                throw new InvalidOperationException(
                    "Grace window has not yet opened (quorum not reached).");
            }

            var graceEndsAt = state.GracePeriodStartedAt.Value + _options.GracePeriod;
            var now = _clock.UtcNow();
            if (now >= graceEndsAt)
            {
                throw new InvalidOperationException(
                    "Grace window has elapsed; dispute can no longer be filed (call EvaluateGracePeriodAsync to finalize).");
            }

            if (!dispute.Verify(state.PendingRequest, _signer))
            {
                throw new ArgumentException("Dispute signature is invalid or does not bind to the pending request.", nameof(dispute));
            }

            var authorized = await _disputerValidator
                .IsAuthorizedAsync(dispute.DisputingPublicKey, cancellationToken)
                .ConfigureAwait(false);
            if (!authorized)
            {
                throw new ArgumentException(
                    "Disputer public key is not authorized; only registered owner identities may dispute a recovery.",
                    nameof(dispute));
            }

            var (evt, hashAfter) = AppendEvent(
                state,
                RecoveryEventType.RecoveryDisputed,
                actorNodeId: dispute.DisputingNodeId,
                targetNodeId: state.PendingRequest.RequestingNodeId,
                occurredAt: now,
                detail: BuildDetail(
                    ("dispute.reason", dispute.Reason ?? string.Empty),
                    ("dispute.disputedAt", dispute.DisputedAt.ToString("O")),
                    ("dispute.signature.hex", Convert.ToHexString(dispute.Signature))));

            var next = CloneStateWith(state, disputed: true, lastEventHash: hashAfter);
            await _store.SaveAsync(next, cancellationToken).ConfigureAwait(false);
            return evt;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<RecoveryEvent?> EvaluateGracePeriodAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var state = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
            if (state.PendingRequest is null) return null;
            if (state.GracePeriodStartedAt is null) return null;
            if (state.Disputed || state.Completed) return null;

            var graceEndsAt = state.GracePeriodStartedAt.Value + _options.GracePeriod;
            var now = _clock.UtcNow();
            if (now < graceEndsAt) return null;

            var (evt, hashAfter) = AppendEvent(
                state,
                RecoveryEventType.RecoveryCompleted,
                actorNodeId: state.PendingRequest.RequestingNodeId,
                targetNodeId: state.PendingRequest.RequestingNodeId,
                occurredAt: now,
                detail: BuildDetail(
                    ("grace.startedAt", state.GracePeriodStartedAt.Value.ToString("O")),
                    ("grace.elapsedAt", graceEndsAt.ToString("O")),
                    ("attestations.count", state.Attestations.Count.ToString())));

            var next = CloneStateWith(state, completed: true, lastEventHash: hashAfter);
            await _store.SaveAsync(next, cancellationToken).ConfigureAwait(false);
            return evt;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<RecoveryStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var state = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
            var kind = ClassifyStatus(state);
            DateTimeOffset? graceElapsesAt = state.GracePeriodStartedAt is { } start
                ? start + _options.GracePeriod
                : null;
            var activeAttestations = 0;
            foreach (var nodeId in state.Attestations.Keys)
            {
                if (state.Trustees.ContainsKey(nodeId)) activeAttestations++;
            }
            return new RecoveryStatus(
                Kind: kind,
                PendingRequest: state.PendingRequest,
                AttestationsReceived: activeAttestations,
                QuorumThreshold: _options.QuorumThreshold,
                GracePeriodStartedAt: state.GracePeriodStartedAt,
                GracePeriodElapsesAt: graceElapsesAt);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static RecoveryStatusKind ClassifyStatus(RecoveryCoordinatorState state)
    {
        if (state.PendingRequest is null) return RecoveryStatusKind.NoRequest;
        if (state.Disputed) return RecoveryStatusKind.Disputed;
        if (state.Completed) return RecoveryStatusKind.Completed;
        if (state.GracePeriodStartedAt is not null) return RecoveryStatusKind.GracePeriodActive;
        return RecoveryStatusKind.AwaitingAttestations;
    }

    private (RecoveryEvent Event, byte[] HashAfter) AppendEvent(
        RecoveryCoordinatorState state,
        RecoveryEventType type,
        string actorNodeId,
        string targetNodeId,
        DateTimeOffset occurredAt,
        IReadOnlyDictionary<string, string> detail)
    {
        var evt = new RecoveryEvent(
            Type: type,
            ActorNodeId: actorNodeId,
            TargetNodeId: targetNodeId,
            OccurredAt: occurredAt,
            PreviousEventHash: state.LastEventHash,
            Detail: detail);
        var hash = ChainHashOf(evt);
        return (evt, hash);
    }

    private static RecoveryCoordinatorState CloneStateWith(
        RecoveryCoordinatorState source,
        IReadOnlyDictionary<string, TrusteeDesignation>? trustees = null,
        bool? disputed = null,
        bool? completed = null,
        byte[]? lastEventHash = null)
    {
        return new RecoveryCoordinatorState
        {
            Trustees = trustees ?? source.Trustees,
            PendingRequest = source.PendingRequest,
            Attestations = source.Attestations,
            GracePeriodStartedAt = source.GracePeriodStartedAt,
            Disputed = disputed ?? source.Disputed,
            Completed = completed ?? source.Completed,
            LastEventHash = lastEventHash ?? source.LastEventHash,
        };
    }

    /// <summary>
    /// SHA-256 over a canonical serialization of <paramref name="evt"/>.
    /// Public for tests and for the audit-log substrate to verify chain
    /// integrity by replaying events against their successors'
    /// <see cref="RecoveryEvent.PreviousEventHash"/>.
    /// </summary>
    public static byte[] ChainHashOf(RecoveryEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);

        using var ms = new MemoryStream();
        var prefix = Encoding.UTF8.GetBytes(EventHashDomainPrefix);
        ms.Write(prefix);
        ms.WriteByte((byte)evt.Type);
        WriteSeparator(ms);
        WriteUtf8(ms, evt.ActorNodeId);
        WriteSeparator(ms);
        WriteUtf8(ms, evt.TargetNodeId);
        WriteSeparator(ms);
        WriteUtf8(ms, evt.OccurredAt.ToString("O"));
        WriteSeparator(ms);
        if (evt.PreviousEventHash is { Length: > 0 } prev)
        {
            ms.Write(prev);
        }
        WriteSeparator(ms);
        // Canonicalize the detail dictionary by sorted-key concatenation.
        foreach (var kv in evt.Detail.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            WriteUtf8(ms, kv.Key);
            ms.WriteByte((byte)'=');
            WriteUtf8(ms, kv.Value);
            ms.WriteByte((byte)';');
        }

        return SHA256.HashData(ms.ToArray());

        static void WriteSeparator(Stream s) => s.WriteByte(FieldSeparator);
        static void WriteUtf8(Stream s, string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            var bytes = Encoding.UTF8.GetBytes(value);
            s.Write(bytes);
        }
    }

    private static IReadOnlyDictionary<string, string> BuildDetail(
        params ReadOnlySpan<(string Key, string Value)> entries)
    {
        if (entries.Length == 0)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
        var dict = new Dictionary<string, string>(entries.Length, StringComparer.Ordinal);
        foreach (var (k, v) in entries)
        {
            dict[k] = v;
        }
        return dict;
    }

    private static void ValidateOptions(RecoveryCoordinatorOptions options)
    {
        if (options.QuorumThreshold < 1)
        {
            throw new ArgumentException(
                $"QuorumThreshold must be >= 1; got {options.QuorumThreshold}.", nameof(options));
        }
        if (options.MaxTrustees < 1)
        {
            throw new ArgumentException(
                $"MaxTrustees must be >= 1; got {options.MaxTrustees}.", nameof(options));
        }
        if (options.QuorumThreshold > options.MaxTrustees)
        {
            throw new ArgumentException(
                $"QuorumThreshold ({options.QuorumThreshold}) cannot exceed MaxTrustees ({options.MaxTrustees}).",
                nameof(options));
        }
        if (options.GracePeriod <= TimeSpan.Zero)
        {
            throw new ArgumentException(
                $"GracePeriod must be positive; got {options.GracePeriod}.", nameof(options));
        }
    }
}
