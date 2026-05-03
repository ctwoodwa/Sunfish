using System.Collections.Immutable;
using System.Threading;
using Sunfish.Foundation.Crypto;
using Sunfish.Kernel.Audit;

namespace Sunfish.Foundation.Taxonomy.Tests;

public sealed class TaxonomyAuditEmissionTests
{
    private static readonly TenantId Tenant = new("tenant-a");
    private static readonly TaxonomyDefinitionId DefId = new("Acme", "Demo", "Things");
    private static readonly CancellationToken Ct = CancellationToken.None;

    private sealed class CapturingAuditTrail : IAuditTrail
    {
        public List<AuditRecord> Records { get; } = new();

        public ValueTask AppendAsync(AuditRecord record, CancellationToken ct = default)
        {
            Records.Add(record);
            return ValueTask.CompletedTask;
        }

        public async IAsyncEnumerable<AuditRecord> QueryAsync(AuditQuery query, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            foreach (var r in Records)
            {
                yield return r;
            }
            await Task.CompletedTask;
        }
    }

    private sealed class StubSigner : IOperationSigner
    {
        public PrincipalId IssuerId { get; } = PrincipalId.FromBytes(new byte[32]);

        public ValueTask<SignedOperation<T>> SignAsync<T>(T payload, DateTimeOffset issuedAt, Guid nonce, CancellationToken ct = default)
        {
            var sig = Signature.FromBytes(new byte[64]);
            return ValueTask.FromResult(new SignedOperation<T>(payload, IssuerId, issuedAt, nonce, sig));
        }
    }

    private static (InMemoryTaxonomyRegistry registry, CapturingAuditTrail trail) NewWithAudit()
    {
        var trail = new CapturingAuditTrail();
        var registry = new InMemoryTaxonomyRegistry(trail, new StubSigner());
        return (registry, trail);
    }

    [Fact]
    public async Task Create_EmitsTaxonomyDefinitionCreated()
    {
        var (registry, trail) = NewWithAudit();
        await registry.CreateAsync(Tenant, DefId, TaxonomyVersion.V1_0_0, TaxonomyGovernanceRegime.Civilian,
            "demo", new ActorId("u1"), null, Ct);

        var record = Assert.Single(trail.Records);
        Assert.Equal(AuditEventType.TaxonomyDefinitionCreated, record.EventType);
        Assert.Equal(Tenant, record.TenantId);
        var body = record.Payload.Payload.Body;
        Assert.Equal(DefId.Value, body["definition_id"]);
        Assert.Equal("Civilian", body["regime"]);
        Assert.Equal("u1", body["owner"]);
    }

    [Fact]
    public async Task PublishVersion_EmitsTaxonomyVersionPublished()
    {
        var (registry, trail) = NewWithAudit();
        await registry.CreateAsync(Tenant, DefId, TaxonomyVersion.V1_0_0, TaxonomyGovernanceRegime.Civilian,
            "demo", new ActorId("u1"), null, Ct);
        await registry.PublishVersionAsync(Tenant, DefId, new TaxonomyVersion(1, 1, 0), new ActorId("u1"), Ct);

        Assert.Equal(2, trail.Records.Count);
        Assert.Equal(AuditEventType.TaxonomyVersionPublished, trail.Records[1].EventType);
        Assert.Equal("1.1.0", trail.Records[1].Payload.Payload.Body["version"]);
    }

    [Fact]
    public async Task Retire_EmitsTaxonomyVersionRetired()
    {
        var (registry, trail) = NewWithAudit();
        await registry.CreateAsync(Tenant, DefId, TaxonomyVersion.V1_0_0, TaxonomyGovernanceRegime.Civilian,
            "demo", new ActorId("u1"), null, Ct);
        await registry.RetireDefinitionVersionAsync(Tenant, DefId, TaxonomyVersion.V1_0_0, new ActorId("u1"), Ct);

        Assert.Equal(2, trail.Records.Count);
        Assert.Equal(AuditEventType.TaxonomyVersionRetired, trail.Records[1].EventType);
    }

    [Fact]
    public async Task AddNode_EmitsTaxonomyNodeAdded()
    {
        var (registry, trail) = NewWithAudit();
        await registry.CreateAsync(Tenant, DefId, TaxonomyVersion.V1_0_0, TaxonomyGovernanceRegime.Civilian,
            "demo", new ActorId("u1"), null, Ct);
        await registry.AddNodeAsync(Tenant, DefId, TaxonomyVersion.V1_0_0,
            "thing-a", "Thing A", "first thing", null, new ActorId("u1"), Ct);

        var add = trail.Records.Single(r => r.EventType == AuditEventType.TaxonomyNodeAdded);
        Assert.Equal("thing-a", add.Payload.Payload.Body["code"]);
        Assert.Null(add.Payload.Payload.Body["parent_code"]);
    }

    [Fact]
    public async Task ReviseDisplay_EmitsTaxonomyNodeDisplayRevised()
    {
        var (registry, trail) = NewWithAudit();
        await registry.CreateAsync(Tenant, DefId, TaxonomyVersion.V1_0_0, TaxonomyGovernanceRegime.Civilian,
            "demo", new ActorId("u1"), null, Ct);
        await registry.AddNodeAsync(Tenant, DefId, TaxonomyVersion.V1_0_0,
            "thing-a", "Thing A", "first thing", null, new ActorId("u1"), Ct);

        var nodeId = new TaxonomyNodeId(DefId, "thing-a");
        await registry.ReviseDisplayAsync(Tenant, nodeId, TaxonomyVersion.V1_0_0,
            "Thing A v2", "rev2", "typo", new ActorId("u1"), Ct);

        var rev = trail.Records.Single(r => r.EventType == AuditEventType.TaxonomyNodeDisplayRevised);
        Assert.Equal("Thing A v2", rev.Payload.Payload.Body["new_display"]);
        Assert.Equal("typo", rev.Payload.Payload.Body["revision_reason"]);
    }

    [Fact]
    public async Task Tombstone_EmitsTaxonomyNodeTombstoned()
    {
        var (registry, trail) = NewWithAudit();
        await registry.CreateAsync(Tenant, DefId, TaxonomyVersion.V1_0_0, TaxonomyGovernanceRegime.Civilian,
            "demo", new ActorId("u1"), null, Ct);
        await registry.AddNodeAsync(Tenant, DefId, TaxonomyVersion.V1_0_0,
            "thing-a", "Thing A", "first thing", null, new ActorId("u1"), Ct);

        var nodeId = new TaxonomyNodeId(DefId, "thing-a");
        await registry.TombstoneNodeAsync(Tenant, nodeId, TaxonomyVersion.V1_0_0,
            "deprecated", "thing-b", new ActorId("u1"), Ct);

        var tomb = trail.Records.Single(r => r.EventType == AuditEventType.TaxonomyNodeTombstoned);
        Assert.Equal("thing-b", tomb.Payload.Payload.Body["successor_code"]);
        Assert.Equal("deprecated", tomb.Payload.Payload.Body["deprecation_reason"]);
    }

    [Fact]
    public async Task Clone_EmitsTaxonomyDefinitionCloned()
    {
        var (registry, trail) = NewWithAudit();
        await registry.CreateAsync(Tenant, DefId, TaxonomyVersion.V1_0_0, TaxonomyGovernanceRegime.Authoritative,
            "demo", ActorId.Sunfish, null, Ct);

        var newId = new TaxonomyDefinitionId("Tenant", "Demo", "Things");
        await registry.CloneAsync(Tenant, DefId, TaxonomyVersion.V1_0_0,
            newId, new ActorId("u1"), "tenant fork", Ct);

        var clone = trail.Records.Single(r => r.EventType == AuditEventType.TaxonomyDefinitionCloned);
        Assert.Equal(newId.Value, clone.Payload.Payload.Body["new_definition_id"]);
        Assert.Equal("tenant fork", clone.Payload.Payload.Body["reason"]);
    }

    [Fact]
    public async Task Extend_EmitsTaxonomyDefinitionExtended()
    {
        var (registry, trail) = NewWithAudit();
        await registry.CreateAsync(Tenant, DefId, TaxonomyVersion.V1_0_0, TaxonomyGovernanceRegime.Authoritative,
            "demo", ActorId.Sunfish, null, Ct);

        var newId = new TaxonomyDefinitionId("Tenant", "Demo", "ThingsExt");
        await registry.ExtendAsync(Tenant, DefId, TaxonomyVersion.V1_0_0,
            newId, new ActorId("u1"), "tenant extension", Ct);

        Assert.Contains(trail.Records, r => r.EventType == AuditEventType.TaxonomyDefinitionExtended);
    }

    [Fact]
    public async Task Alter_EmitsTaxonomyDefinitionAltered()
    {
        var (registry, trail) = NewWithAudit();
        await registry.CreateAsync(Tenant, DefId, TaxonomyVersion.V1_0_0, TaxonomyGovernanceRegime.Authoritative,
            "demo", ActorId.Sunfish, null, Ct);

        var newId = new TaxonomyDefinitionId("Tenant", "Demo", "ThingsAlt");
        await registry.AlterAsync(Tenant, DefId, TaxonomyVersion.V1_0_0,
            newId, new ActorId("u1"), "regulatory shift", Ct);

        Assert.Contains(trail.Records, r => r.EventType == AuditEventType.TaxonomyDefinitionAltered);
    }
}
