using System.IO;
using System.Runtime.InteropServices;
using Sunfish.Kernel.Runtime.Teams;

namespace Sunfish.Kernel.Runtime.Tests;

/// <summary>
/// Pin-down tests for <see cref="TeamPaths"/>. Every assertion is a literal
/// string built from <see cref="Path.Combine"/> + <see cref="Path.DirectorySeparatorChar"/>
/// so the tests pass on Windows and POSIX runners alike while still catching
/// a refactor that (a) changes casing on the <c>teams/</c>, <c>events/</c>, or
/// <c>buckets/</c> segments, (b) drops the nested <c>{teamId}</c> directory,
/// (c) URL-encodes or hyphen-strips the GUID, or (d) re-punctuates the
/// keystore key name. No I/O — these are pure-function tests.
/// </summary>
public sealed class TeamPathsTests
{
    // Fixed team id so every path assertion is a literal string.
    private static readonly TeamId SampleTeamId =
        TeamId.Parse("11111111-1111-1111-1111-111111111111");

    private const string SampleTeamIdString = "11111111-1111-1111-1111-111111111111";

    // Parameterise over Windows- and POSIX-shaped base paths so we catch a
    // refactor that breaks on either separator convention.
    public static IEnumerable<object[]> BaseDirectories =>
        new[]
        {
            new object[] { @"C:\data" },
            new object[] { "/tmp/data" },
        };

    [Theory]
    [MemberData(nameof(BaseDirectories))]
    public void TeamRoot_combines_dataDirectory_teams_and_teamId(string dataDirectory)
    {
        var expected = Path.Combine(dataDirectory, "teams", SampleTeamIdString);

        var actual = TeamPaths.TeamRoot(dataDirectory, SampleTeamId);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(BaseDirectories))]
    public void DatabasePath_is_teamRoot_slash_sunfishDb(string dataDirectory)
    {
        var expected = Path.Combine(dataDirectory, "teams", SampleTeamIdString, "sunfish.db");

        var actual = TeamPaths.DatabasePath(dataDirectory, SampleTeamId);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(BaseDirectories))]
    public void EventLogDirectory_is_teamRoot_slash_events(string dataDirectory)
    {
        var expected = Path.Combine(dataDirectory, "teams", SampleTeamIdString, "events");

        var actual = TeamPaths.EventLogDirectory(dataDirectory, SampleTeamId);

        Assert.Equal(expected, actual);
        // Directory helpers never return a trailing separator — callers add
        // their own as needed.
        Assert.DoesNotMatch(@"[\\/]+$", actual);
    }

    [Theory]
    [MemberData(nameof(BaseDirectories))]
    public void BucketsDirectory_is_teamRoot_slash_buckets(string dataDirectory)
    {
        var expected = Path.Combine(dataDirectory, "teams", SampleTeamIdString, "buckets");

        var actual = TeamPaths.BucketsDirectory(dataDirectory, SampleTeamId);

        Assert.Equal(expected, actual);
        Assert.DoesNotMatch(@"[\\/]+$", actual);
    }

    [Fact]
    public void KeystoreKeyName_is_literal_sunfish_colon_team_colon_guid_colon_primary()
    {
        // Pinned string — colons, lowercase, GUID "D" format, trailing ":primary".
        // Any deviation (case, punctuation, GUID "N" form, etc.) must fail this test.
        const string expected = "sunfish:team:11111111-1111-1111-1111-111111111111:primary";

        var actual = TeamPaths.KeystoreKeyName(SampleTeamId);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void KeystoreKeyName_is_independent_of_dataDirectory()
    {
        // Regression guard: the keystore entry is install-level naming; it
        // must not accidentally start interpolating a path.
        var actual = TeamPaths.KeystoreKeyName(TeamId.Parse("00000000-0000-0000-0000-000000000000"));
        Assert.Equal("sunfish:team:00000000-0000-0000-0000-000000000000:primary", actual);
    }

    [Theory]
    [MemberData(nameof(BaseDirectories))]
    public void LegacyBackupDirectory_has_no_team_segment(string dataDirectory)
    {
        var expected = Path.Combine(dataDirectory, "legacy-backup");

        var actual = TeamPaths.LegacyBackupDirectory(dataDirectory);

        Assert.Equal(expected, actual);
        // It must NOT have a teams/ segment — v1 data predates per-team.
        Assert.DoesNotContain("teams", actual);
    }

    [Fact]
    public void Two_different_TeamIds_produce_different_team_roots()
    {
        var a = TeamId.Parse("11111111-1111-1111-1111-111111111111");
        var b = TeamId.Parse("22222222-2222-2222-2222-222222222222");

        Assert.NotEqual(TeamPaths.TeamRoot(@"C:\data", a), TeamPaths.TeamRoot(@"C:\data", b));
        Assert.NotEqual(TeamPaths.DatabasePath(@"C:\data", a), TeamPaths.DatabasePath(@"C:\data", b));
        Assert.NotEqual(TeamPaths.EventLogDirectory(@"C:\data", a), TeamPaths.EventLogDirectory(@"C:\data", b));
        Assert.NotEqual(TeamPaths.BucketsDirectory(@"C:\data", a), TeamPaths.BucketsDirectory(@"C:\data", b));
        Assert.NotEqual(TeamPaths.KeystoreKeyName(a), TeamPaths.KeystoreKeyName(b));
    }

    [Fact]
    public void TeamRoot_throws_on_empty_dataDirectory()
    {
        Assert.Throws<ArgumentException>(() => TeamPaths.TeamRoot("", SampleTeamId));
    }

    [Fact]
    public void DatabasePath_throws_on_empty_dataDirectory()
    {
        Assert.Throws<ArgumentException>(() => TeamPaths.DatabasePath("", SampleTeamId));
    }

    [Fact]
    public void EventLogDirectory_throws_on_empty_dataDirectory()
    {
        Assert.Throws<ArgumentException>(() => TeamPaths.EventLogDirectory("", SampleTeamId));
    }

    [Fact]
    public void BucketsDirectory_throws_on_empty_dataDirectory()
    {
        Assert.Throws<ArgumentException>(() => TeamPaths.BucketsDirectory("", SampleTeamId));
    }

    [Fact]
    public void LegacyBackupDirectory_throws_on_empty_dataDirectory()
    {
        Assert.Throws<ArgumentException>(() => TeamPaths.LegacyBackupDirectory(""));
    }

    [Fact]
    public void TransportEndpoint_is_platform_specific_and_includes_teamId()
    {
        // Wave 6.3.C: per-team transport endpoints. The concrete shape is
        // platform-dependent (Unix-domain socket vs named pipe), so assert
        // via the same platform switch the helper uses.
        var endpoint = TeamPaths.TransportEndpoint("/tmp/data", SampleTeamId);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.Equal(@"\\.\pipe\sunfish-" + SampleTeamIdString, endpoint);
            // Windows pipes are flat-namespace — dataDirectory must NOT be
            // interpolated into the pipe name.
            Assert.DoesNotContain("tmp", endpoint);
            Assert.DoesNotContain("data", endpoint);
        }
        else
        {
            var expected = Path.Combine("/tmp/data", "teams", SampleTeamIdString, "sync.sock");
            Assert.Equal(expected, endpoint);
        }
    }

    [Fact]
    public void TransportEndpoint_throws_on_empty_dataDirectory()
    {
        Assert.Throws<ArgumentException>(() => TeamPaths.TransportEndpoint("", SampleTeamId));
    }

    [Fact]
    public void TransportEndpoint_is_distinct_per_team()
    {
        var a = TeamId.Parse("11111111-1111-1111-1111-111111111111");
        var b = TeamId.Parse("22222222-2222-2222-2222-222222222222");

        Assert.NotEqual(
            TeamPaths.TransportEndpoint(@"C:\data", a),
            TeamPaths.TransportEndpoint(@"C:\data", b));
    }

    [Fact]
    public void TeamId_segment_is_rendered_in_GUID_D_form_not_N_or_B_or_P()
    {
        // Guard against a refactor that swaps ToString("D") for ToString("N")
        // (no-hyphens), ToString("B") (braces), or ToString("P") (parens).
        // All helpers must render hyphens and NO wrapping punctuation.
        var dataDirectory = @"C:\data";
        var root = TeamPaths.TeamRoot(dataDirectory, SampleTeamId);

        Assert.Contains(SampleTeamIdString, root);
        Assert.DoesNotContain("{", root);
        Assert.DoesNotContain("}", root);
        Assert.DoesNotContain("(", root);
        Assert.DoesNotContain(")", root);
        // GUID "N" form strips hyphens — reject it.
        Assert.DoesNotContain("11111111111111111111111111111111", root);
    }
}
