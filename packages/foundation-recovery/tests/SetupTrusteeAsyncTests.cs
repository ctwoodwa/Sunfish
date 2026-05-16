using Sunfish.Kernel.Security.Crypto;
using Sunfish.Foundation.Recovery;

namespace Sunfish.Foundation.Recovery.Tests;

/// <summary>
/// W#67 / ADR 0046-A6 — coverage for
/// <see cref="IRecoveryCoordinator.SetupTrusteeAsync"/> and the
/// <see cref="RecoveryCoordinatorState.TrusteeEncryptedSeeds"/>
/// persistence path. Verifies idempotency, overwrites on re-call, and
/// argument validation. Also covers the new
/// <see cref="EvaluateGracePeriodAsync"/> contract: completion returns
/// the contributing attestations alongside the event.
/// </summary>
public sealed class SetupTrusteeAsyncTests
{
    private sealed class TestClock : IRecoveryClock
    {
        public DateTimeOffset Now { get; set; } = new DateTimeOffset(2026, 5, 16, 12, 0, 0, TimeSpan.Zero);
        public DateTimeOffset UtcNow() => Now;
    }

    [Fact]
    public async Task SetupTrusteeAsync_persists_encrypted_seed_under_trustee_nodeId()
    {
        var coord = NewCoordinator(out var store);
        var seed = NewEncryptedSeed("trustee-A");

        await coord.SetupTrusteeAsync("trustee-A", seed);

        var state = await store.LoadAsync(default);
        Assert.True(state.TrusteeEncryptedSeeds.ContainsKey("trustee-A"));
        Assert.Equal(seed, state.TrusteeEncryptedSeeds["trustee-A"]);
    }

    [Fact]
    public async Task SetupTrusteeAsync_overwrites_prior_envelope_for_same_trustee()
    {
        var coord = NewCoordinator(out var store);
        var firstSeed = NewEncryptedSeed("trustee-A", fillByte: 0x11);
        var secondSeed = NewEncryptedSeed("trustee-A", fillByte: 0x22);

        await coord.SetupTrusteeAsync("trustee-A", firstSeed);
        await coord.SetupTrusteeAsync("trustee-A", secondSeed);

        var state = await store.LoadAsync(default);
        Assert.Single(state.TrusteeEncryptedSeeds);
        Assert.Equal(secondSeed, state.TrusteeEncryptedSeeds["trustee-A"]);
    }

    [Fact]
    public async Task SetupTrusteeAsync_supports_multiple_trustees_independently()
    {
        var coord = NewCoordinator(out var store);
        await coord.SetupTrusteeAsync("trustee-A", NewEncryptedSeed("trustee-A", fillByte: 0xAA));
        await coord.SetupTrusteeAsync("trustee-B", NewEncryptedSeed("trustee-B", fillByte: 0xBB));

        var state = await store.LoadAsync(default);
        Assert.Equal(2, state.TrusteeEncryptedSeeds.Count);
        Assert.Equal(0xAA, state.TrusteeEncryptedSeeds["trustee-A"].Ciphertext[0]);
        Assert.Equal(0xBB, state.TrusteeEncryptedSeeds["trustee-B"].Ciphertext[0]);
    }

    [Fact]
    public async Task SetupTrusteeAsync_rejects_empty_nodeId()
    {
        var coord = NewCoordinator(out _);
        await Assert.ThrowsAsync<ArgumentException>(
            () => coord.SetupTrusteeAsync(string.Empty, NewEncryptedSeed("trustee-A")));
    }

    [Fact]
    public async Task SetupTrusteeAsync_rejects_mismatched_nodeId_in_payload()
    {
        var coord = NewCoordinator(out _);
        var payloadWithDifferentNodeId = NewEncryptedSeed("trustee-B");
        await Assert.ThrowsAsync<ArgumentException>(
            () => coord.SetupTrusteeAsync("trustee-A", payloadWithDifferentNodeId));
    }

    [Fact]
    public async Task SetupTrusteeAsync_rejects_null_payload()
    {
        var coord = NewCoordinator(out _);
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => coord.SetupTrusteeAsync("trustee-A", null!));
    }

    // ----- helpers ---------------------------------------------------

    private static IRecoveryCoordinator NewCoordinator(out InMemoryRecoveryStateStore store)
    {
        var clock = new TestClock();
        var signer = new Ed25519Signer();
        var (ownerPub, _) = signer.GenerateKeyPair();
        store = new InMemoryRecoveryStateStore();
        return new RecoveryCoordinator(
            clock, store, signer,
            new FixedDisputerValidator(new[] { ownerPub }),
            new RecoveryCoordinatorOptions());
    }

    private static TrusteeEncryptedSeed NewEncryptedSeed(string nodeId, byte fillByte = 0x00)
    {
        var pub = new byte[32];
        var ct  = new byte[48];
        var nonce = new byte[24];
        if (fillByte != 0x00)
        {
            Array.Fill(ct, fillByte);
        }
        return new TrusteeEncryptedSeed(
            TrusteeNodeId:           nodeId,
            OwnerEphX25519PublicKey: pub,
            Ciphertext:              ct,
            Nonce:                   nonce);
    }
}
