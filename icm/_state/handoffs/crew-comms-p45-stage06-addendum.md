# W#45 P4.5 — Crew Comms follow-up (TYPING + DELIVERED + transcript alignment + glare-wiring)

**From:** XO research session  
**To:** sunfish-PM (COB) session  
**Workstream:** W#45 — Crew Comms (addendum to `foundation-channels-crew-comms-stage06-handoff.md`)  
**ADR:** `docs/adrs/0076-crew-comms-foundation-channels.md` (Accepted; A1 PR #564 + A2 PR #566)  
**Authored:** 2026-05-05  
**Pipeline variant:** `sunfish-api-change` (IChannelSession interface expands)  
**Estimated effort:** ~4–6h / 3 PRs  
**Pre-merge council:** mandatory for PR 1 (security — transcript hash) and PR 3 (glare-wiring). PR 2 (TYPING/DELIVERED) standard review.

---

## Context

W#45 P1–P5 shipped (LAN text substrate). Four items deferred to this addendum:

1. **Transcript-hash alignment** (A1+A2) — `ComputeTranscriptHash` does not yet include `presence.caps` (A1) or `INVITE.capabilities` (A2). Security-critical; closes relay-MitM capability-downgrade vectors.
2. **TYPING indicator** (Phase 1 acceptance criterion #5) — send/receive `0x07 TYPING {}`.
3. **DELIVERED acknowledgment** (Phase 1 acceptance criterion #4) — send/receive `0x08 DELIVERED { messageId: bytes[16] }`.
4. **Glare-wiring** — when two peers simultaneously call `OpenAsync` to each other, `NativeChannelProvider` must deterministically deduplicate the resulting sessions using `GlareResolver.IsLocalYielder`.

---

## Halt conditions

1. `packages/blocks-crew-comms/Crypto/EncryptionHandshake.cs` has changed since this hand-off was authored — re-read `ComputeTranscriptHash` and verify the current signature before adding parameters.
2. `packages/blocks-crew-comms/Signaling/HandshakeFlow.cs` `InitiatorPostHelloAsync` or `ResponderAcceptAsync` methods have changed — re-read both before making call-site edits.
3. `packages/foundation-channels/IChannelSession.cs` already has `SendTypingAsync` or `SendDeliveredAsync` — verify before adding them.
4. Any other `IChannelSession` implementor besides `NativeChannelSession` has appeared in `packages/blocks-crew-comms/` — must update it too.
5. `NativeChannelProvider._identity` is not of type `KeyPair` or `PeerId.From(_identity.PrincipalId)` throws — stop and file COB question.

---

## PR 1 — Transcript-hash alignment (A1 + A2) — SECURITY — pre-merge council

**Files:**
- `packages/blocks-crew-comms/Crypto/EncryptionHandshake.cs`
- `packages/blocks-crew-comms/Signaling/HandshakeFlow.cs`
- `packages/blocks-crew-comms/Signaling/SessionListener.cs` (one call site in `DeferredInvitation.AcceptAsync`)
- `packages/blocks-crew-comms/tests/Crypto/EncryptionHandshakeTests.cs`

### 1.1 Extend `EncryptionHandshake.ComputeTranscriptHash`

Current 6-param signature:
```csharp
public static byte[] ComputeTranscriptHash(
    ReadOnlySpan<byte> initiatorHelloEphemeral,
    ReadOnlySpan<byte> initiatorHelloIdentity,
    ReadOnlySpan<byte> responderHelloEphemeral,
    ReadOnlySpan<byte> responderHelloIdentity,
    ReadOnlySpan<byte> tenantIdBytes,
    byte negotiatedCapability)
```

Replace with 9-param signature (A1 + A2 additions):
```csharp
/// <summary>
/// Computes the SHA-256 transcript hash per ADR 0076 §A2.3 §A1 ext.
/// Full canonical form:
/// SHA-256(ephemA[32] || idA[32] || ephemB[32] || idB[32]
///         || uint32BE(len(tenantBytes)) || tenantBytes
///         || inviteCaps[1]         — A2: INVITE.capabilities
///         || negotiatedCap[1]      — ACCEPT.capability
///         || presenceCapsA[1]      — A1: HELLO_A.presence.caps
///         || presenceCapsB[1])     — A1: HELLO_B.presence.caps
/// </summary>
public static byte[] ComputeTranscriptHash(
    ReadOnlySpan<byte> initiatorHelloEphemeral,
    ReadOnlySpan<byte> initiatorHelloIdentity,
    ReadOnlySpan<byte> responderHelloEphemeral,
    ReadOnlySpan<byte> responderHelloIdentity,
    ReadOnlySpan<byte> tenantIdBytes,
    byte inviteCapabilities,
    byte negotiatedCapability,
    byte presenceCapsInitiator,
    byte presenceCapsResponder)
```

Buffer change — replace the current `var totalLen = ... + 1` with `... + 4` and append 4 bytes at the end:

```csharp
// Replace the last 3 lines of the buffer-fill block with:
buffer[offset++] = inviteCapabilities;
buffer[offset++] = negotiatedCapability;
buffer[offset++] = presenceCapsInitiator;
buffer[offset] = presenceCapsResponder;
```

### 1.2 Update `HandshakeFlow.InitiatorPostHelloAsync`

The call to `ComputeTranscriptHash` is currently:
```csharp
var transcript = EncryptionHandshake.ComputeTranscriptHash(
    localHello.EphemeralPublicKey, localHello.IdentityPublicKey,
    remoteHello.EphemeralPublicKey, remoteHello.IdentityPublicKey,
    tenantBytes, accept.Capability);
```

Replace with:
```csharp
var transcript = EncryptionHandshake.ComputeTranscriptHash(
    localHello.EphemeralPublicKey, localHello.IdentityPublicKey,
    remoteHello.EphemeralPublicKey, remoteHello.IdentityPublicKey,
    tenantBytes,
    (byte)preferredCapabilities,  // inviteCapabilities — what we sent in INVITE
    accept.Capability,             // negotiatedCapability — what remote sent in ACCEPT
    localHello.Presence.Caps,      // presenceCapsInitiator — our HELLO.presence.caps
    remoteHello.Presence.Caps);    // presenceCapsResponder — remote's HELLO.presence.caps
```

### 1.3 Update `HandshakeFlow.ResponderAcceptAsync`

Current signature:
```csharp
public static async Task ResponderAcceptAsync(
    FrameProtocol frames,
    ChannelCapability negotiated,
    HelloPayload initiatorHello,
    HelloPayload responderHello,
    TenantId tenantId,
    CancellationToken ct)
```

Add `byte offeredCapabilities` parameter (the `INVITE.capabilities` byte the initiator sent):
```csharp
public static async Task ResponderAcceptAsync(
    FrameProtocol frames,
    ChannelCapability negotiated,
    byte offeredCapabilities,       // INVITE.capabilities from initiator — new A2 param
    HelloPayload initiatorHello,
    HelloPayload responderHello,
    TenantId tenantId,
    CancellationToken ct)
```

Update the `ComputeTranscriptHash` call inside this method:
```csharp
var transcript = EncryptionHandshake.ComputeTranscriptHash(
    initiatorHello.EphemeralPublicKey, initiatorHello.IdentityPublicKey,
    responderHello.EphemeralPublicKey, responderHello.IdentityPublicKey,
    tenantBytes,
    offeredCapabilities,           // inviteCapabilities
    (byte)negotiated,              // negotiatedCapability
    initiatorHello.Presence.Caps,  // presenceCapsInitiator
    responderHello.Presence.Caps); // presenceCapsResponder
```

### 1.4 Update `DeferredInvitation.AcceptAsync` in `SessionListener.cs`

The call site:
```csharp
await HandshakeFlow.ResponderAcceptAsync(
    _frames, _negotiated, _initiatorHello, _responderHello, _tenantId, ct)
    .ConfigureAwait(false);
```

Replace with:
```csharp
await HandshakeFlow.ResponderAcceptAsync(
    _frames, _negotiated, (byte)OfferedCapabilities, _initiatorHello, _responderHello, _tenantId, ct)
    .ConfigureAwait(false);
```

`OfferedCapabilities` is already stored on `DeferredInvitation` (the `offered` parameter from the constructor).

### 1.5 Update known-answer test for CONFIRM transcript

In `EncryptionHandshakeTests.cs` (or wherever the A1.7 known-answer test lives), find the test computing a transcript hash and add the 3 new fixture bytes. The new fixture should produce a different hash than the old one — update the expected constant accordingly. Run with `dotnet test` to verify.

The test currently passes 6 args; update to 9:
```csharp
var hash = EncryptionHandshake.ComputeTranscriptHash(
    ephemA, idA, ephemB, idB, tenantBytes,
    inviteCaps: 0x07,            // new — any non-zero fixture value
    negotiatedCap: 0x01,
    presenceCapsA: 0x01,         // new — A's HELLO.presence.caps
    presenceCapsB: 0x01);        // new — B's HELLO.presence.caps
```

Recompute the expected hash (use `SHA256.HashData` inline in the test with the full concatenated buffer per the new spec; verify the hand-coded expected bytes match).

---

## PR 2 — TYPING indicator + DELIVERED acknowledgment — standard review

**Files:**
- `packages/foundation-channels/IChannelSession.cs`
- `packages/blocks-crew-comms/Session/NativeChannelSession.cs`
- `packages/blocks-crew-comms/tests/Session/NativeChannelSessionTypingDeliveredTests.cs` (new)

### 2.1 Extend `IChannelSession`

Add four new members:

```csharp
/// <summary>
/// Sends a typing indicator to the remote peer (frame 0x07 TYPING).
/// ADR 0076 specifies suppression of 3s after the last keystroke — the
/// caller is responsible for the cooldown timer; this method sends
/// unconditionally on each call.
/// </summary>
Task SendTypingAsync(CancellationToken ct);

/// <summary>
/// Sends a DELIVERED receipt for the given message. The receiver MUST call
/// this once per TEXT frame received. Message ID is encoded as RFC 4122
/// big-endian UUID (16 bytes), per ADR 0076 §Wire protocol frame 0x08.
/// </summary>
Task SendDeliveredAsync(Guid messageId, CancellationToken ct);

/// <summary>
/// Streams timestamps for typing-indicator frames received from the remote
/// peer. Single-consumer only — enumerating from multiple consumers
/// concurrently is undefined behavior.
/// </summary>
IAsyncEnumerable<DateTimeOffset> ReceiveTypingAsync(CancellationToken ct);

/// <summary>
/// Streams message IDs from DELIVERED-receipt frames received from the
/// remote peer. Single-consumer only.
/// </summary>
IAsyncEnumerable<Guid> ReceiveDeliveredAsync(CancellationToken ct);
```

### 2.2 Implement in `NativeChannelSession`

**New channels** (add alongside `_inboundText`):
```csharp
// Bounded at 8; drop-oldest so a typing storm doesn't grow unbounded.
private readonly Channel<DateTimeOffset> _inboundTyping =
    Channel.CreateBounded<DateTimeOffset>(new BoundedChannelOptions(8)
    {
        FullMode = BoundedChannelFullMode.DropOldest,
        SingleWriter = true,
        SingleReader = true,
    });

private readonly Channel<Guid> _inboundDelivered =
    Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true });
```

**Reader pump** — extend the `case MessageType.Typing:` and `case MessageType.Delivered:` branches (currently both fall through to `break` silently):

```csharp
case MessageType.Typing:
    _inboundTyping.Writer.TryWrite(_time.GetUtcNow());
    break;

case MessageType.Delivered:
    if (payload.Length == 16)
    {
        var guid = RFC4122GuidFormatter.FromBytes(payload.Span);
        _inboundDelivered.Writer.TryWrite(guid);
    }
    break;
```

**`SendTypingAsync`:**
```csharp
public Task SendTypingAsync(CancellationToken ct)
{
    EnsureActive();
    return _frames.WriteFrameAsync(MessageType.Typing, ReadOnlyMemory<byte>.Empty, ct);
}
```

**`SendDeliveredAsync`:**
```csharp
public Task SendDeliveredAsync(Guid messageId, CancellationToken ct)
{
    EnsureActive();
    var bytes = RFC4122GuidFormatter.ToBytes(messageId);
    return _frames.WriteFrameAsync(MessageType.Delivered, bytes, ct);
}
```

**`ReceiveTypingAsync` / `ReceiveDeliveredAsync`:**
```csharp
public IAsyncEnumerable<DateTimeOffset> ReceiveTypingAsync(CancellationToken ct)
    => _inboundTyping.Reader.ReadAllAsync(ct);

public IAsyncEnumerable<Guid> ReceiveDeliveredAsync(CancellationToken ct)
    => _inboundDelivered.Reader.ReadAllAsync(ct);
```

**`Terminate` cleanup** — add channel completions alongside `_inboundText.Writer.TryComplete()`:
```csharp
_inboundTyping.Writer.TryComplete();
_inboundDelivered.Writer.TryComplete();
```

**`RFC4122GuidFormatter` note** — a `RFC4122GuidFormatter.cs` already exists in `packages/blocks-crew-comms/Protocol/`. Add `ToBytes(Guid)` and `FromBytes(ReadOnlySpan<byte>)` helpers there if they don't exist already; the RFC 4122 encoding is big-endian UUID (Time_low + Time_mid + Time_hi_and_version + Clock_seq_hi + Clock_seq_low + Node — 16 bytes BE). `System.Guid` in .NET is little-endian for the first three groups; swap manually.

### 2.3 Tests

In `NativeChannelSessionTypingDeliveredTests.cs`:

```
a. TypingIndicator_ReceivesTimestamp
   - Wire two NativeChannelSession instances via MemoryStream pair
   - Sender calls SendTypingAsync
   - Receiver's ReceiveTypingAsync yields a DateTimeOffset
   - Assert: timestamp ≥ test start

b. DeliveredAck_RoundTrip
   - Wire two sessions
   - Sender sends TEXT (arbitrary message ID)
   - Receiver calls SendDeliveredAsync with the message GUID
   - Sender's ReceiveDeliveredAsync yields the same GUID

c. Typing_DropOldest_NoBoundViolation
   - Wire two sessions
   - Sender calls SendTypingAsync 12 times rapidly
   - Receiver drains ReceiveTypingAsync — should receive ≤8 items (no throw)
```

---

## PR 3 — Glare-wiring — pre-merge council

**Files:**
- `packages/blocks-crew-comms/NativeChannelProvider.cs`
- `packages/blocks-crew-comms/Signaling/SessionInitiator.cs` (minor)
- `packages/blocks-crew-comms/tests/GlareResolutionTests.cs` (new)

### 3.1 Design

Glare = both peers call `OpenAsync` simultaneously, creating two concurrent sessions to each other. The fix is at `NativeChannelProvider`: track in-flight outbound handshakes per peer, intercept the arriving invitation from the same peer, and deterministically keep only one session.

No protocol wire changes are needed — `GlareResolver.IsLocalYielder` already exists. The wiring is purely at the provider coordination layer.

### 3.2 `NativeChannelProvider` changes

Add private state:
```csharp
private readonly System.Collections.Concurrent.ConcurrentDictionary<PeerId, TaskCompletionSource<IChannelSession>> _pendingOutbounds
    = new();
```

**Modified `OpenAsync`:**
```csharp
public async Task<IChannelSession> OpenAsync(
    TenantId tenant, PeerId peer, ChannelCapability preferredCapabilities, CancellationToken ct)
{
    var localPeer = PeerId.From(_identity.PrincipalId);
    var tcs = new TaskCompletionSource<IChannelSession>(TaskCreationOptions.RunContinuationsAsynchronously);

    // Register outbound in-flight. If a concurrent inbound from `peer` arrives,
    // the ListenAsync pump will resolve this TCS with the inbound session instead.
    _pendingOutbounds[peer] = tcs;
    try
    {
        // Race: try the outbound; if inbound wins, tcs is resolved by ListenAsync.
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var outboundTask = _initiator.OpenAsync(tenant, peer, preferredCapabilities, linkedCts.Token);
        var inboundTask = tcs.Task;

        var winner = await Task.WhenAny(outboundTask, inboundTask).ConfigureAwait(false);

        if (winner == inboundTask)
        {
            // Glare: inbound arrived first; cancel the outbound.
            linkedCts.Cancel();
            try { await outboundTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { /* expected */ }
            catch { /* discard; inbound wins */ }
            return await inboundTask.ConfigureAwait(false);
        }

        // Outbound won; discard the tcs (may still be pending, but we won't use it).
        tcs.TrySetCanceled();
        return await outboundTask.ConfigureAwait(false);
    }
    finally
    {
        _pendingOutbounds.TryRemove(peer, out _);
    }
}
```

**Modified `ListenAsync`:**

Replace the current passthrough:
```csharp
public IAsyncEnumerable<IChannelInvitation> ListenAsync(TenantId tenant, CancellationToken ct)
    => _listener.ListenAsync(tenant, ct);
```

With a glare-checking wrapper:
```csharp
public async IAsyncEnumerable<IChannelInvitation> ListenAsync(
    TenantId tenant,
    [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
{
    var localPeer = PeerId.From(_identity.PrincipalId);
    await foreach (var invitation in _listener.ListenAsync(tenant, ct).ConfigureAwait(false))
    {
        if (_pendingOutbounds.TryGetValue(invitation.FromPeer, out var tcs))
        {
            // Glare: an outbound to this peer is in-flight.
            if (GlareResolver.IsLocalYielder(localPeer, invitation.FromPeer))
            {
                // We yield — accept the inbound and hand it to the pending OpenAsync.
                try
                {
                    var session = await invitation.AcceptAsync(ct).ConfigureAwait(false);
                    tcs.TrySetResult(session);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
                // Do NOT yield this invitation to the caller — it's consumed by glare resolution.
            }
            else
            {
                // We win — reject the inbound; our outbound will complete normally.
                try { await invitation.RejectAsync("Glare-reject: local peer wins.", ct).ConfigureAwait(false); }
                catch { /* best-effort */ }
            }
        }
        else
        {
            yield return invitation;
        }
    }
}
```

### 3.3 Tests

In `GlareResolutionTests.cs`:

```
a. GlareResolution_LowerPeerYields_InboundSessionReturned
   - Create two NativeChannelProvider instances: providerA (lower PeerId) + providerB (higher PeerId)
   - Wire them via in-memory IDuplexStream pair factory (both sides connect simultaneously)
   - Call providerA.OpenAsync(peer=B) and providerB.OpenAsync(peer=A) in parallel (Task.WhenAll)
   - Assert: both Task<IChannelSession> complete without throw
   - Assert: sessions are Active; providerA.OpenAsync returned the inbound session (via glare resolution)
   - Assert: no duplicate sessions (each side has exactly 1 session to the other)

b. GlareResolution_HigherPeerWins_OutboundSessionReturned
   - Same setup as (a) but verify providerB (winner) gets its outbound session
   - providerA (yielder) gets the inbound (via tcs resolution in ListenAsync)

c. NoGlare_NormalOpenAsync_UnaffectedByPendingOutbounds
   - providerA.OpenAsync(peer=C) — no glare (no concurrent inbound from C)
   - Assert: normal outbound completes; _pendingOutbounds cleans up on exit
```

**Implementation constraint:** the test needs an in-memory `IDuplexStream` pair. Use or create a `MemoryDuplexStream` helper in the test assembly (similar to what P1–P4 tests used for FrameProtocol tests).

### 3.4 Commit message guidance

```
feat(crew-comms): W#45 P4.5c — glare-wiring via NativeChannelProvider coordination
```

---

## Acceptance criteria (P4.5 complete)

1. `EncryptionHandshake.ComputeTranscriptHash` has 9 parameters; known-answer test passes with updated fixture.
2. `IChannelSession` exposes `SendTypingAsync`, `SendDeliveredAsync`, `ReceiveTypingAsync`, `ReceiveDeliveredAsync`.
3. Integration test: peer A sends TYPING, peer B's `ReceiveTypingAsync` yields a timestamp within 100ms.
4. Integration test: peer A sends TEXT with a Guid, peer B calls `SendDeliveredAsync(guid)`, peer A's `ReceiveDeliveredAsync` yields the same Guid.
5. Glare test: two providers with in-memory streams, both call `OpenAsync` concurrently → exactly one session per peer pair, no throws.
6. `dotnet test packages/blocks-crew-comms/tests` — all tests green.
7. Pre-merge council on PR 1 and PR 3; standard council on PR 2.
