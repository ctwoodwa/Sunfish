using Sunfish.Kernel.Buckets;
using Sunfish.Kernel.Security.Attestation;

namespace Sunfish.Kernel.Buckets.Tests;

public sealed class BucketRegistryTests
{
    private static BucketDefinition Def(string name, string requiredAttestation, ReplicationMode mode = ReplicationMode.Eager) =>
        new(
            Name: name,
            RecordTypes: new[] { "projects" },
            Filter: null,
            Replication: mode,
            RequiredAttestation: requiredAttestation,
            MaxLocalAgeDays: null);

    private static RoleAttestation Att(string role) =>
        new(
            TeamId: new byte[RoleAttestation.TeamIdLength],
            SubjectPublicKey: new byte[RoleAttestation.PublicKeyLength],
            Role: role,
            IssuedAt: DateTimeOffset.UtcNow.AddDays(-1),
            ExpiresAt: DateTimeOffset.UtcNow.AddDays(30),
            IssuerPublicKey: new byte[RoleAttestation.PublicKeyLength],
            Signature: new byte[RoleAttestation.SignatureLength]);

    [Fact]
    public void Register_stores_the_definition()
    {
        var registry = new BucketRegistry();
        var def = Def("team_core", "team_member");

        registry.Register(def);

        Assert.Single(registry.Definitions);
        Assert.Same(def, registry.Definitions[0]);
    }

    [Fact]
    public void Find_returns_registered_definition_by_name()
    {
        var registry = new BucketRegistry();
        registry.Register(Def("team_core", "team_member"));
        registry.Register(Def("financial_records", "financial_role"));

        var found = registry.Find("financial_records");

        Assert.NotNull(found);
        Assert.Equal("financial_role", found!.RequiredAttestation);
    }

    [Fact]
    public void Find_returns_null_for_unknown_bucket()
    {
        var registry = new BucketRegistry();
        registry.Register(Def("team_core", "team_member"));

        Assert.Null(registry.Find("nonexistent"));
    }

    [Fact]
    public void Register_rejects_duplicate_names()
    {
        var registry = new BucketRegistry();
        registry.Register(Def("team_core", "team_member"));

        Assert.Throws<InvalidOperationException>(() =>
            registry.Register(Def("team_core", "admin")));
    }

    [Fact]
    public void EligibleBucketsFor_returns_matching_bucket_when_role_attestation_matches()
    {
        var registry = new BucketRegistry();
        registry.Register(Def("team_core", "team_member"));
        registry.Register(Def("financial_records", "financial_role"));

        var result = registry.EligibleBucketsFor(new[] { Att("team_member") });

        Assert.Single(result);
        Assert.Equal("team_core", result[0].Name);
    }

    [Fact]
    public void EligibleBucketsFor_returns_empty_when_no_attestation_matches()
    {
        var registry = new BucketRegistry();
        registry.Register(Def("team_core", "team_member"));
        registry.Register(Def("financial_records", "financial_role"));

        var result = registry.EligibleBucketsFor(new[] { Att("visitor") });

        Assert.Empty(result);
    }

    [Fact]
    public void EligibleBucketsFor_returns_union_for_peer_with_multiple_attestations()
    {
        var registry = new BucketRegistry();
        registry.Register(Def("team_core", "team_member"));
        registry.Register(Def("financial_records", "financial_role"));
        registry.Register(Def("admin_audit", "admin"));

        var result = registry.EligibleBucketsFor(new[] { Att("team_member"), Att("financial_role") });

        var names = result.Select(d => d.Name).OrderBy(s => s).ToArray();
        Assert.Equal(new[] { "financial_records", "team_core" }, names);
    }
}
