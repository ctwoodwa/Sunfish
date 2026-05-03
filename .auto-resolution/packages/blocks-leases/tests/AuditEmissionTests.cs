using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Sunfish.Blocks.Leases.Models;
using Sunfish.Blocks.Leases.Services;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Kernel.Audit;
using Xunit;

namespace Sunfish.Blocks.Leases.Tests;

/// <summary>
/// W#27 Phase 5: lifecycle audit emission. Verifies LeaseDrafted on Create
/// and the 5 phase-transition events (LeaseExecuted / LeaseActivated /
/// LeaseRenewed / LeaseTerminated / LeaseCancelled). The 3 events for
/// Phases 2 + 3 (LeaseDocumentVersionAppended / LeasePartySignatureRecorded
/// / LeaseLandlordAttestationSet) are declared in kernel-audit but not
/// wired here — covered when Phases 2 + 3 ship.
/// </summary>
public class AuditEmissionTests
{
    private static readonly TenantId TestTenant = new("tenant-a");
    private static readonly ActorId Operator = new("operator");

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

    private static (InMemoryLeaseService svc, CapturingAuditTrail trail) NewWiredService()
    {
        var trail = new CapturingAuditTrail();
        var svc = new InMemoryLeaseService(trail, new StubSigner(), TestTenant);
        return (svc, trail);
    }

    private static CreateLeaseRequest MakeRequest() => new()
    {
        UnitId = new EntityId("unit", "test", "u-1"),
        Tenants = [new PartyId("tenant-a")],
        Landlord = new PartyId("landlord-x"),
        StartDate = new DateOnly(2025, 1, 1),
        EndDate = new DateOnly(2025, 12, 31),
        MonthlyRent = 1500m,
    };

    [Fact]
    public async Task CreateAsync_Emits_LeaseDrafted()
    {
        var (svc, trail) = NewWiredService();
        await svc.CreateAsync(MakeRequest());

        Assert.Single(trail.Records);
        Assert.Equal(AuditEventType.LeaseDrafted, trail.Records[0].EventType);
        Assert.Equal(TestTenant, trail.Records[0].TenantId);
    }

    [Fact]
    public async Task TransitionToExecuted_Emits_LeaseExecuted()
    {
        var (svc, trail) = NewWiredService();
        var lease = await svc.CreateAsync(MakeRequest());
        await svc.TransitionPhaseAsync(lease.Id, LeasePhase.AwaitingSignature, Operator);
        await svc.TransitionPhaseAsync(lease.Id, LeasePhase.Executed, Operator);

        // Drafted + (no event for Draft→AwaitingSignature) + Executed
        Assert.Equal(2, trail.Records.Count);
        Assert.Equal(AuditEventType.LeaseExecuted, trail.Records[1].EventType);
    }

    [Fact]
    public async Task TransitionToActive_Emits_LeaseActivated()
    {
        var (svc, trail) = NewWiredService();
        var lease = await svc.CreateAsync(MakeRequest());
        await svc.TransitionPhaseAsync(lease.Id, LeasePhase.AwaitingSignature, Operator);
        await svc.TransitionPhaseAsync(lease.Id, LeasePhase.Executed, Operator);
        await svc.TransitionPhaseAsync(lease.Id, LeasePhase.Active, Operator);

        Assert.Equal(AuditEventType.LeaseActivated, trail.Records[^1].EventType);
    }

    [Fact]
    public async Task TransitionToRenewed_Emits_LeaseRenewed()
    {
        var (svc, trail) = NewWiredService();
        var lease = await svc.CreateAsync(MakeRequest());
        await svc.TransitionPhaseAsync(lease.Id, LeasePhase.AwaitingSignature, Operator);
        await svc.TransitionPhaseAsync(lease.Id, LeasePhase.Executed, Operator);
        await svc.TransitionPhaseAsync(lease.Id, LeasePhase.Active, Operator);
        await svc.TransitionPhaseAsync(lease.Id, LeasePhase.Renewed, Operator);

        Assert.Equal(AuditEventType.LeaseRenewed, trail.Records[^1].EventType);
    }

    [Fact]
    public async Task TransitionToTerminated_Emits_LeaseTerminated()
    {
        var (svc, trail) = NewWiredService();
        var lease = await svc.CreateAsync(MakeRequest());
        await svc.TransitionPhaseAsync(lease.Id, LeasePhase.AwaitingSignature, Operator);
        await svc.TransitionPhaseAsync(lease.Id, LeasePhase.Executed, Operator);
        await svc.TransitionPhaseAsync(lease.Id, LeasePhase.Active, Operator);
        await svc.TransitionPhaseAsync(lease.Id, LeasePhase.Terminated, Operator);

        Assert.Equal(AuditEventType.LeaseTerminated, trail.Records[^1].EventType);
    }

    [Fact]
    public async Task TransitionToCancelled_Emits_LeaseCancelled()
    {
        var (svc, trail) = NewWiredService();
        var lease = await svc.CreateAsync(MakeRequest());
        await svc.TransitionPhaseAsync(lease.Id, LeasePhase.Cancelled, Operator);

        Assert.Equal(AuditEventType.LeaseCancelled, trail.Records[^1].EventType);
    }

    [Fact]
    public async Task NoEmission_When_AuditTrailUnwired()
    {
        // Parameterless ctor disables emission; should not throw.
        var svc = new InMemoryLeaseService();
        var lease = await svc.CreateAsync(MakeRequest());
        await svc.TransitionPhaseAsync(lease.Id, LeasePhase.Cancelled, Operator);
        // No assertion possible without trail; smoke test that nothing throws.
        Assert.Equal(LeasePhase.Cancelled, (await svc.GetAsync(lease.Id))!.Phase);
    }

    [Fact]
    public async Task PayloadBody_ContainsLeaseId_AndActor()
    {
        var (svc, trail) = NewWiredService();
        var lease = await svc.CreateAsync(MakeRequest());
        await svc.TransitionPhaseAsync(lease.Id, LeasePhase.Cancelled, Operator);

        var cancelledRecord = trail.Records[^1];
        var body = cancelledRecord.Payload.Payload.Body;
        Assert.Equal(lease.Id.Value, body["lease_id"]);
        Assert.Equal(Operator.Value, body["actor"]);
    }

    [Fact]
    public async Task TenantAttribution_AppliedToAllRecords()
    {
        var (svc, trail) = NewWiredService();
        var lease = await svc.CreateAsync(MakeRequest());
        await svc.TransitionPhaseAsync(lease.Id, LeasePhase.AwaitingSignature, Operator);
        await svc.TransitionPhaseAsync(lease.Id, LeasePhase.Executed, Operator);

        Assert.All(trail.Records, r => Assert.Equal(TestTenant, r.TenantId));
    }
}
