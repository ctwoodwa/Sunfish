using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Sunfish.Blocks.Leases.Models;
using Sunfish.Blocks.Leases.Services;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Kernel.Audit;
using Sunfish.Kernel.Signatures.Models;
using Xunit;

namespace Sunfish.Blocks.Leases.Tests;

/// <summary>
/// W#27 Phases 2+3 — LeaseDocumentVersion append-only log + per-party
/// signatures + landlord attestation + Executed-transition guard.
/// </summary>
public sealed class DocumentVersionAndSignaturesTests
{
    private static readonly TenantId TestTenant = new("tenant-a");
    private static readonly ActorId Operator = new("operator");
    private static readonly PartyId TenantAlice = new("tenant-alice");
    private static readonly PartyId TenantBob = new("tenant-bob");

    private sealed class CapturingAuditTrail : IAuditTrail
    {
        public List<AuditRecord> Records { get; } = new();
        public ValueTask AppendAsync(AuditRecord record, CancellationToken ct = default)
        {
            Records.Add(record);
            return ValueTask.CompletedTask;
        }
        public async IAsyncEnumerable<AuditRecord> QueryAsync(AuditQuery query, [EnumeratorCancellation] CancellationToken ct = default)
        {
            foreach (var r in Records) yield return r;
            await Task.CompletedTask;
        }
    }

    private sealed class StubSigner : IOperationSigner
    {
        public PrincipalId IssuerId { get; } = PrincipalId.FromBytes(new byte[32]);
        public ValueTask<SignedOperation<T>> SignAsync<T>(T payload, DateTimeOffset issuedAt, Guid nonce, CancellationToken ct = default) =>
            ValueTask.FromResult(new SignedOperation<T>(payload, IssuerId, issuedAt, nonce, Signature.FromBytes(new byte[64])));
    }

    private static (InMemoryLeaseService svc, CapturingAuditTrail trail, InMemoryLeaseDocumentVersionLog log) NewService()
    {
        var trail = new CapturingAuditTrail();
        var log = new InMemoryLeaseDocumentVersionLog();
        var svc = new InMemoryLeaseService(trail, new StubSigner(), TestTenant, log);
        return (svc, trail, log);
    }

    private static CreateLeaseRequest MakeRequest(IReadOnlyList<PartyId>? tenants = null) => new()
    {
        UnitId = new EntityId("unit", "test", "u-1"),
        Tenants = tenants ?? new[] { TenantAlice },
        Landlord = new PartyId("landlord-x"),
        StartDate = new DateOnly(2026, 6, 1),
        EndDate = new DateOnly(2027, 5, 31),
        MonthlyRent = 2500m,
    };

    private static LeaseDocumentVersion MakeRevision(LeaseId leaseId) => new()
    {
        Id = default,                                       // assigned on append
        Lease = leaseId,                                    // overwritten on append
        VersionNumber = 0,                                  // assigned on append
        DocumentHash = ContentHash.ComputeFromUtf8Nfc("doc body v1"),
        DocumentBlobRef = "blob://test/lease-v1",
        AuthoredBy = Operator,
        AuthoredAt = DateTimeOffset.UtcNow,
        ChangeSummary = "Initial draft",
    };

    // ─────────── LeaseDocumentVersion append ───────────

    [Fact]
    public async Task AppendDocumentVersion_AssignsMonotonicVersionNumber()
    {
        var (svc, _, log) = NewService();
        var lease = await svc.CreateAsync(MakeRequest());

        var lease1 = await svc.AppendDocumentVersionAsync(lease.Id, MakeRevision(lease.Id), Operator);
        var lease2 = await svc.AppendDocumentVersionAsync(lease.Id, MakeRevision(lease.Id), Operator);

        Assert.Equal(2, lease2.DocumentVersions.Count);
        var versions = new List<LeaseDocumentVersion>();
        await foreach (var v in log.ListAsync(lease.Id, default)) versions.Add(v);
        Assert.Equal(1, versions[0].VersionNumber);
        Assert.Equal(2, versions[1].VersionNumber);
    }

    [Fact]
    public async Task AppendDocumentVersion_EmitsAuditEvent()
    {
        var (svc, trail, _) = NewService();
        var lease = await svc.CreateAsync(MakeRequest());

        await svc.AppendDocumentVersionAsync(lease.Id, MakeRevision(lease.Id), Operator);

        Assert.Contains(trail.Records, r => r.EventType == AuditEventType.LeaseDocumentVersionAppended);
    }

    [Fact]
    public async Task AppendDocumentVersion_WithoutLog_Throws()
    {
        var trail = new CapturingAuditTrail();
        var svc = new InMemoryLeaseService(trail, new StubSigner(), TestTenant);  // no log
        var lease = await svc.CreateAsync(MakeRequest());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AppendDocumentVersionAsync(lease.Id, MakeRevision(lease.Id), Operator).AsTask());
    }

    [Fact]
    public async Task DocumentVersionLog_GetLatest_ReturnsHighestVersionNumber()
    {
        var log = new InMemoryLeaseDocumentVersionLog();
        var leaseId = LeaseId.NewId();
        await log.AppendAsync(MakeRevision(leaseId), default);
        await log.AppendAsync(MakeRevision(leaseId) with { ChangeSummary = "Revised" }, default);

        var latest = await log.GetLatestAsync(leaseId, default);
        Assert.NotNull(latest);
        Assert.Equal(2, latest!.VersionNumber);
    }

    // ─────────── Per-party signatures ───────────

    [Fact]
    public async Task RecordPartySignature_AppendsToLeaseAndEmits()
    {
        var (svc, trail, _) = NewService();
        var lease = await svc.CreateAsync(MakeRequest());
        await svc.AppendDocumentVersionAsync(lease.Id, MakeRevision(lease.Id), Operator);

        var sigEventId = new SignatureEventId(Guid.NewGuid());
        var updated = await svc.RecordPartySignatureAsync(lease.Id, TenantAlice, sigEventId, Operator);

        Assert.Single(updated.PartySignatures);
        Assert.Equal(TenantAlice, updated.PartySignatures[0].Party);
        Assert.Equal(sigEventId, updated.PartySignatures[0].SignatureEvent);
        Assert.Contains(trail.Records, r => r.EventType == AuditEventType.LeasePartySignatureRecorded);
    }

    [Fact]
    public async Task RecordPartySignature_NonTenant_Throws()
    {
        var (svc, _, _) = NewService();
        var lease = await svc.CreateAsync(MakeRequest());
        await svc.AppendDocumentVersionAsync(lease.Id, MakeRevision(lease.Id), Operator);

        var stranger = new PartyId("stranger");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.RecordPartySignatureAsync(lease.Id, stranger, new SignatureEventId(Guid.NewGuid()), Operator).AsTask());
    }

    [Fact]
    public async Task RecordPartySignature_BeforeAnyVersion_Throws()
    {
        var (svc, _, _) = NewService();
        var lease = await svc.CreateAsync(MakeRequest());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.RecordPartySignatureAsync(lease.Id, TenantAlice, new SignatureEventId(Guid.NewGuid()), Operator).AsTask());
    }

    [Fact]
    public async Task RecordPartySignature_BindsToLatestVersion()
    {
        var (svc, _, log) = NewService();
        var lease = await svc.CreateAsync(MakeRequest());
        await svc.AppendDocumentVersionAsync(lease.Id, MakeRevision(lease.Id), Operator);
        await svc.AppendDocumentVersionAsync(lease.Id, MakeRevision(lease.Id) with { ChangeSummary = "v2" }, Operator);

        var updated = await svc.RecordPartySignatureAsync(lease.Id, TenantAlice, new SignatureEventId(Guid.NewGuid()), Operator);

        var latest = await log.GetLatestAsync(lease.Id, default);
        Assert.Equal(latest!.Id, updated.PartySignatures[0].DocumentVersion);
    }

    // ─────────── Landlord attestation ───────────

    [Fact]
    public async Task SetLandlordAttestation_PersistsAndEmits()
    {
        var (svc, trail, _) = NewService();
        var lease = await svc.CreateAsync(MakeRequest());

        var sigEventId = new SignatureEventId(Guid.NewGuid());
        var updated = await svc.SetLandlordAttestationAsync(lease.Id, sigEventId, Operator);

        Assert.Equal(sigEventId, updated.LandlordAttestation);
        Assert.Contains(trail.Records, r => r.EventType == AuditEventType.LeaseLandlordAttestationSet);
    }

    // ─────────── AwaitingSignature → Executed guard ───────────

    [Fact]
    public async Task ExecutedGuard_AllowsLegacyFlow_WhenNoVersionAppended()
    {
        // Backward-compat: leases authored without the version-tracking
        // flow can still transition (existing 38 tests rely on this).
        var (svc, _, _) = NewService();
        var lease = await svc.CreateAsync(MakeRequest());
        await svc.TransitionPhaseAsync(lease.Id, LeasePhase.AwaitingSignature, Operator);

        var executed = await svc.TransitionPhaseAsync(lease.Id, LeasePhase.Executed, Operator);
        Assert.Equal(LeasePhase.Executed, executed.Phase);
    }

    [Fact]
    public async Task ExecutedGuard_RejectsWhenAttestationMissing()
    {
        var (svc, _, _) = NewService();
        var lease = await svc.CreateAsync(MakeRequest());
        await svc.AppendDocumentVersionAsync(lease.Id, MakeRevision(lease.Id), Operator);
        await svc.RecordPartySignatureAsync(lease.Id, TenantAlice, new SignatureEventId(Guid.NewGuid()), Operator);
        await svc.TransitionPhaseAsync(lease.Id, LeasePhase.AwaitingSignature, Operator);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.TransitionPhaseAsync(lease.Id, LeasePhase.Executed, Operator).AsTask());
        Assert.Contains("landlord attestation", ex.Message);
    }

    [Fact]
    public async Task ExecutedGuard_RejectsWhenTenantHasNotSigned()
    {
        var (svc, _, _) = NewService();
        var lease = await svc.CreateAsync(MakeRequest(new[] { TenantAlice, TenantBob }));
        await svc.AppendDocumentVersionAsync(lease.Id, MakeRevision(lease.Id), Operator);
        await svc.RecordPartySignatureAsync(lease.Id, TenantAlice, new SignatureEventId(Guid.NewGuid()), Operator);
        // TenantBob has not signed.
        await svc.SetLandlordAttestationAsync(lease.Id, new SignatureEventId(Guid.NewGuid()), Operator);
        await svc.TransitionPhaseAsync(lease.Id, LeasePhase.AwaitingSignature, Operator);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.TransitionPhaseAsync(lease.Id, LeasePhase.Executed, Operator).AsTask());
        Assert.Contains(TenantBob.Value, ex.Message);
    }

    [Fact]
    public async Task ExecutedGuard_AllowsAfterAllSignaturesAndAttestation()
    {
        var (svc, _, _) = NewService();
        var lease = await svc.CreateAsync(MakeRequest(new[] { TenantAlice, TenantBob }));
        await svc.AppendDocumentVersionAsync(lease.Id, MakeRevision(lease.Id), Operator);
        await svc.RecordPartySignatureAsync(lease.Id, TenantAlice, new SignatureEventId(Guid.NewGuid()), Operator);
        await svc.RecordPartySignatureAsync(lease.Id, TenantBob, new SignatureEventId(Guid.NewGuid()), Operator);
        await svc.SetLandlordAttestationAsync(lease.Id, new SignatureEventId(Guid.NewGuid()), Operator);
        await svc.TransitionPhaseAsync(lease.Id, LeasePhase.AwaitingSignature, Operator);

        var executed = await svc.TransitionPhaseAsync(lease.Id, LeasePhase.Executed, Operator);
        Assert.Equal(LeasePhase.Executed, executed.Phase);
    }

    [Fact]
    public async Task ExecutedGuard_RejectsWhenSignatureIsForOldVersion()
    {
        var (svc, _, _) = NewService();
        var lease = await svc.CreateAsync(MakeRequest());
        await svc.AppendDocumentVersionAsync(lease.Id, MakeRevision(lease.Id), Operator);
        // Sign v1.
        await svc.RecordPartySignatureAsync(lease.Id, TenantAlice, new SignatureEventId(Guid.NewGuid()), Operator);
        // Append v2 — invalidates Alice's signature on v1.
        await svc.AppendDocumentVersionAsync(lease.Id, MakeRevision(lease.Id) with { ChangeSummary = "v2 — substantive" }, Operator);
        await svc.SetLandlordAttestationAsync(lease.Id, new SignatureEventId(Guid.NewGuid()), Operator);
        await svc.TransitionPhaseAsync(lease.Id, LeasePhase.AwaitingSignature, Operator);

        // Guard rejects: Alice's signature is for v1, but v2 is the latest.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.TransitionPhaseAsync(lease.Id, LeasePhase.Executed, Operator).AsTask());
    }
}
