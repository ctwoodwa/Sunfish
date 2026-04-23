using Sunfish.Kernel.Buckets;

namespace Sunfish.Kernel.Buckets.Tests;

public sealed class BucketYamlLoaderTests
{
    private readonly IBucketYamlLoader _loader = new BucketYamlLoader();

    [Fact]
    public void Parses_paper_10_2_example()
    {
        const string yaml = """
                            buckets:
                              - name: team_core
                                record_types: [projects, tasks, members, comments]
                                filter: record.team_id = peer.team_id
                                replication: eager
                                required_attestation: team_member

                              - name: archived_projects
                                record_types: [projects, tasks]
                                filter: project.archived = true
                                replication: lazy
                                required_attestation: team_member
                                max_local_age_days: 90
                            """;

        var defs = _loader.LoadFrom(yaml);

        Assert.Equal(2, defs.Count);

        var core = defs[0];
        Assert.Equal("team_core", core.Name);
        Assert.Equal(new[] { "projects", "tasks", "members", "comments" }, core.RecordTypes);
        Assert.Equal("record.team_id = peer.team_id", core.Filter);
        Assert.Equal(ReplicationMode.Eager, core.Replication);
        Assert.Equal("team_member", core.RequiredAttestation);
        Assert.Null(core.MaxLocalAgeDays);

        var archived = defs[1];
        Assert.Equal("archived_projects", archived.Name);
        Assert.Equal(ReplicationMode.Lazy, archived.Replication);
        Assert.Equal(90, archived.MaxLocalAgeDays);
        Assert.Equal("project.archived = true", archived.Filter);
    }

    [Fact]
    public void Rejects_malformed_yaml()
    {
        const string malformed = "buckets:\n  - name: missing_closing_bracket\n    record_types: [a, b";

        Assert.Throws<BucketYamlException>(() => _loader.LoadFrom(malformed));
    }

    [Fact]
    public void Defaults_replication_mode_to_eager_when_omitted()
    {
        const string yaml = """
                            buckets:
                              - name: default_repl
                                record_types: [foo]
                                required_attestation: team_member
                            """;

        var defs = _loader.LoadFrom(yaml);

        Assert.Single(defs);
        Assert.Equal(ReplicationMode.Eager, defs[0].Replication);
    }

    [Fact]
    public void Parses_multiple_buckets_in_one_document()
    {
        const string yaml = """
                            buckets:
                              - name: a
                                record_types: [x]
                                replication: eager
                                required_attestation: team_member
                              - name: b
                                record_types: [y]
                                replication: lazy
                                required_attestation: financial_role
                              - name: c
                                record_types: [z]
                                replication: eager
                                required_attestation: admin
                            """;

        var defs = _loader.LoadFrom(yaml);

        Assert.Equal(3, defs.Count);
        Assert.Equal(new[] { "a", "b", "c" }, defs.Select(d => d.Name));
        Assert.Equal(ReplicationMode.Lazy, defs[1].Replication);
        Assert.Equal("financial_role", defs[1].RequiredAttestation);
    }

    [Fact]
    public void Throws_when_required_fields_missing()
    {
        const string missingName = """
                                   buckets:
                                     - record_types: [foo]
                                       required_attestation: team_member
                                   """;

        var ex = Assert.Throws<BucketYamlException>(() => _loader.LoadFrom(missingName));
        Assert.Contains("name", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Throws_when_replication_value_unknown()
    {
        const string yaml = """
                            buckets:
                              - name: bad_repl
                                record_types: [foo]
                                replication: maybe
                                required_attestation: team_member
                            """;

        var ex = Assert.Throws<BucketYamlException>(() => _loader.LoadFrom(yaml));
        Assert.Contains("replication", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
