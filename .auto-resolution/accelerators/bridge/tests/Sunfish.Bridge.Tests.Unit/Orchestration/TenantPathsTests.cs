using System.IO;
using Sunfish.Bridge.Orchestration;
using Xunit;

namespace Sunfish.Bridge.Tests.Unit.Orchestration;

/// <summary>
/// Pins the literal-string path conventions declared in
/// <c>_shared/product/wave-5.2-decomposition.md</c> §5 "Tenant Data-Dir
/// Layout — Lock-In". These tests exist so that any future refactor of
/// <see cref="TenantPaths"/> has to explicitly re-state the convention —
/// silent drift produces a test failure, not a silent production
/// mis-layout of tenant disks.
/// </summary>
public class TenantPathsTests
{
    private static readonly Guid TenantA = new("11111111-2222-3333-4444-555555555555");
    private static readonly Guid TenantB = new("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    [Fact]
    public void TenantRoot_ComposesTenantsSegmentAndGuidInDForm()
    {
        var actual = TenantPaths.TenantRoot(@"C:\data", TenantA);

        var expected = Path.Combine(@"C:\data", "tenants", "11111111-2222-3333-4444-555555555555");
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void NodeDataDirectory_AppendsNodeSegmentUnderTenantRoot()
    {
        var actual = TenantPaths.NodeDataDirectory(@"C:\data", TenantA);

        var expected = Path.Combine(
            @"C:\data",
            "tenants",
            "11111111-2222-3333-4444-555555555555",
            "node");
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void GraveyardRoot_UsesYyyyMMddHHmmssTimestampSegment()
    {
        var cancelledAt = new DateTimeOffset(2026, 04, 23, 15, 30, 45, TimeSpan.Zero);

        var actual = TenantPaths.GraveyardRoot(@"C:\data", TenantA, cancelledAt);

        // Format is literal yyyyMMdd-HHmmss — test asserts the exact segment
        // so any format drift fails here before it corrupts graveyard ordering.
        Assert.Contains("20260423-153045", actual);
        var expected = Path.Combine(
            @"C:\data",
            "graveyard",
            "11111111-2222-3333-4444-555555555555",
            "20260423-153045");
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void DistinctTenantIds_ProduceDistinctPaths()
    {
        var rootA = TenantPaths.TenantRoot(@"C:\data", TenantA);
        var rootB = TenantPaths.TenantRoot(@"C:\data", TenantB);
        var nodeA = TenantPaths.NodeDataDirectory(@"C:\data", TenantA);
        var nodeB = TenantPaths.NodeDataDirectory(@"C:\data", TenantB);

        Assert.NotEqual(rootA, rootB);
        Assert.NotEqual(nodeA, nodeB);
    }

    [Fact]
    public void PosixStyleBasePath_ComposesCorrectly()
    {
        // Per decomposition plan §5 the POSIX default root is
        // /var/lib/sunfish/bridge/tenants — exercise the helper with a
        // forward-slash base to prove it doesn't hard-code the platform
        // separator in its own logic (Path.Combine handles that).
        const string posixBase = "/var/lib/sunfish/bridge";

        var root = TenantPaths.TenantRoot(posixBase, TenantA);
        var node = TenantPaths.NodeDataDirectory(posixBase, TenantA);
        var cancelledAt = new DateTimeOffset(2026, 04, 23, 15, 30, 45, TimeSpan.Zero);
        var grave = TenantPaths.GraveyardRoot(posixBase, TenantA, cancelledAt);

        Assert.Equal(
            Path.Combine(posixBase, "tenants", "11111111-2222-3333-4444-555555555555"),
            root);
        Assert.Equal(
            Path.Combine(posixBase, "tenants", "11111111-2222-3333-4444-555555555555", "node"),
            node);
        Assert.Equal(
            Path.Combine(posixBase, "graveyard", "11111111-2222-3333-4444-555555555555", "20260423-153045"),
            grave);
    }

    [Fact]
    public void NullOrEmptyDataRoot_Throws()
    {
        Assert.Throws<ArgumentException>(() => TenantPaths.TenantRoot("", TenantA));
        Assert.Throws<ArgumentException>(() => TenantPaths.NodeDataDirectory("", TenantA));
        Assert.Throws<ArgumentException>(
            () => TenantPaths.GraveyardRoot("", TenantA, DateTimeOffset.UtcNow));
    }
}
