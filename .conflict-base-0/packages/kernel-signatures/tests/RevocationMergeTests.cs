using Sunfish.Foundation.Assets.Common;
using Sunfish.Kernel.Signatures.Models;
using Sunfish.Kernel.Signatures.Services;
using Xunit;

namespace Sunfish.Kernel.Signatures.Tests;

/// <summary>
/// W#21 Phase 3 — concurrent-revocation merge per ADR 0054 amendments
/// A4 + A5. Exercises the pure projection directly (independent of any
/// storage implementation) plus the InMemory log's delegation behavior.
/// </summary>
public sealed class RevocationMergeTests
{
    private static readonly ActorId Operator = new("operator");

    private static SignatureRevocation Revoke(SignatureEventId sigId, DateTimeOffset at, RevocationReason reason = RevocationReason.SignerRequest, RevocationEventId? id = null) =>
        new()
        {
            Id = id ?? new RevocationEventId(Guid.NewGuid()),
            SignatureEvent = sigId,
            RevokedAt = at,
            RevokedBy = Operator,
            Reason = reason,
        };

    // ─────────── RevocationProjection.Project (pure) ───────────

    [Fact]
    public void Project_EmptyInput_IsValid()
    {
        var status = RevocationProjection.Project(Array.Empty<SignatureRevocation>());
        Assert.True(status.IsValid);
        Assert.Null(status.RevokedBy);
    }

    [Fact]
    public void Project_SingleEntry_InvalidWithThatEntry()
    {
        var sigId = new SignatureEventId(Guid.NewGuid());
        var revocation = Revoke(sigId, DateTimeOffset.UtcNow);
        var status = RevocationProjection.Project(new[] { revocation });

        Assert.False(status.IsValid);
        Assert.Equal(revocation.Id, status.RevokedBy!.Id);
    }

    [Fact]
    public void Project_TwoEntries_LaterRevokedAtWins()
    {
        var sigId = new SignatureEventId(Guid.NewGuid());
        var earlier = Revoke(sigId, new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero));
        var later = Revoke(sigId, new DateTimeOffset(2026, 5, 2, 12, 0, 0, TimeSpan.Zero), RevocationReason.OperatorError);

        var status = RevocationProjection.Project(new[] { earlier, later });

        Assert.False(status.IsValid);
        Assert.Equal(later.Id, status.RevokedBy!.Id);
        Assert.Equal(RevocationReason.OperatorError, status.RevokedBy.Reason);
    }

    [Fact]
    public void Project_TieOnRevokedAt_TotalOrderTieBreak()
    {
        // Two revocations from offline devices that recorded the same
        // wall-clock at concurrent moments; merge MUST converge to a
        // deterministic winner via total-order on Guid.
        var sigId = new SignatureEventId(Guid.NewGuid());
        var sameTime = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

        // Construct deterministic Guids so the test is reproducible.
        var lowerGuid = new Guid("00000000-0000-0000-0000-000000000001");
        var higherGuid = new Guid("ffffffff-ffff-ffff-ffff-ffffffffffff");

        var lower = Revoke(sigId, sameTime, id: new RevocationEventId(lowerGuid));
        var higher = Revoke(sigId, sameTime, id: new RevocationEventId(higherGuid));

        // Order shouldn't matter — projection sorts internally.
        var status1 = RevocationProjection.Project(new[] { lower, higher });
        var status2 = RevocationProjection.Project(new[] { higher, lower });

        // Whichever Guid wins the CompareTo wins both projections —
        // merge is associative + commutative.
        Assert.Equal(status1.RevokedBy!.Id, status2.RevokedBy!.Id);
    }

    [Fact]
    public void Project_OrderingInputDoesNotMatter_Commutative()
    {
        var sigId = new SignatureEventId(Guid.NewGuid());
        var a = Revoke(sigId, new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero));
        var b = Revoke(sigId, new DateTimeOffset(2026, 5, 1, 13, 0, 0, TimeSpan.Zero));
        var c = Revoke(sigId, new DateTimeOffset(2026, 5, 1, 14, 0, 0, TimeSpan.Zero));

        // Three permutations should all converge.
        var verdict1 = RevocationProjection.Project(new[] { a, b, c });
        var verdict2 = RevocationProjection.Project(new[] { c, a, b });
        var verdict3 = RevocationProjection.Project(new[] { b, c, a });

        Assert.Equal(verdict1.RevokedBy!.Id, verdict2.RevokedBy!.Id);
        Assert.Equal(verdict1.RevokedBy!.Id, verdict3.RevokedBy!.Id);
        Assert.Equal(c.Id, verdict1.RevokedBy.Id);
    }

    [Fact]
    public void Project_ConcurrentOfflineRevocations_ConvergeOnSync()
    {
        // Scenario: two property managers revoke the same signature
        // while offline (e.g., different region offices). When their
        // logs sync, the projection on the merged log must produce
        // the same verdict everywhere.
        var sigId = new SignatureEventId(Guid.NewGuid());
        var deviceAEntry = Revoke(sigId, new DateTimeOffset(2026, 5, 1, 14, 30, 0, TimeSpan.Zero), RevocationReason.Coerced);
        var deviceBEntry = Revoke(sigId, new DateTimeOffset(2026, 5, 1, 14, 35, 0, TimeSpan.Zero), RevocationReason.OperatorError);

        // Merged in either order on either device — same verdict.
        var deviceAView = RevocationProjection.Project(new[] { deviceAEntry, deviceBEntry });
        var deviceBView = RevocationProjection.Project(new[] { deviceBEntry, deviceAEntry });

        Assert.Equal(deviceAView.RevokedBy!.Id, deviceBView.RevokedBy!.Id);
        Assert.Equal(deviceBEntry.Id, deviceAView.RevokedBy.Id); // later RevokedAt wins
    }

    [Fact]
    public void Project_RejectsNullEntries()
    {
        Assert.Throws<ArgumentNullException>(() => RevocationProjection.Project(null!));
    }

    // ─────────── ComparePartialOrder ───────────

    [Fact]
    public void Compare_LaterRevokedAt_Wins()
    {
        var sig = new SignatureEventId(Guid.NewGuid());
        var earlier = Revoke(sig, new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero));
        var later = Revoke(sig, new DateTimeOffset(2026, 5, 1, 13, 0, 0, TimeSpan.Zero));
        Assert.True(RevocationProjection.ComparePartialOrder(later, earlier) > 0);
        Assert.True(RevocationProjection.ComparePartialOrder(earlier, later) < 0);
    }

    [Fact]
    public void Compare_TieOnTime_GuidWins()
    {
        var sig = new SignatureEventId(Guid.NewGuid());
        var same = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
        var lo = Revoke(sig, same, id: new RevocationEventId(new Guid("00000000-0000-0000-0000-000000000001")));
        var hi = Revoke(sig, same, id: new RevocationEventId(new Guid("ffffffff-ffff-ffff-ffff-ffffffffffff")));

        // Sign of the comparison must match Guid.CompareTo — exact
        // direction depends on Guid byte-order semantics, but it MUST
        // be deterministic + consistent with the underlying primitive.
        var direct = lo.Id.Value.CompareTo(hi.Id.Value);
        Assert.Equal(Math.Sign(direct), Math.Sign(RevocationProjection.ComparePartialOrder(lo, hi)));
    }

    [Fact]
    public void Compare_RejectsNulls()
    {
        var sig = new SignatureEventId(Guid.NewGuid());
        var any = Revoke(sig, DateTimeOffset.UtcNow);
        Assert.Throws<ArgumentNullException>(() => RevocationProjection.ComparePartialOrder(null!, any));
        Assert.Throws<ArgumentNullException>(() => RevocationProjection.ComparePartialOrder(any, null!));
    }

    // ─────────── InMemorySignatureRevocationLog delegation ───────────

    [Fact]
    public async Task InMemoryLog_DelegatesToProjection_ConcurrentScenario()
    {
        var log = new InMemorySignatureRevocationLog();
        var sigId = new SignatureEventId(Guid.NewGuid());
        var earlier = Revoke(sigId, new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero));
        var later = Revoke(sigId, new DateTimeOffset(2026, 5, 2, 12, 0, 0, TimeSpan.Zero), RevocationReason.Coerced);

        await log.AppendAsync(earlier, default);
        await log.AppendAsync(later, default);

        var status = await log.GetCurrentValidityAsync(sigId, default);

        Assert.False(status.IsValid);
        Assert.Equal(later.Id, status.RevokedBy!.Id);
        Assert.Equal(RevocationReason.Coerced, status.RevokedBy.Reason);
    }

    [Fact]
    public async Task InMemoryLog_RevokeNonexistentSignature_GracefulValid()
    {
        var log = new InMemorySignatureRevocationLog();
        var status = await log.GetCurrentValidityAsync(new SignatureEventId(Guid.NewGuid()), default);
        Assert.True(status.IsValid);
        Assert.Null(status.RevokedBy);
    }
}
