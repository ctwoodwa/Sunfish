using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;
using Sunfish.Blocks.Properties.Models;
using Sunfish.Blocks.Properties.Services;
using Sunfish.Bridge.Cockpit;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Authorization;
using Xunit;

namespace Sunfish.Bridge.Tests.Unit.Cockpit;

/// <summary>
/// W#29 Phase 1 — route-guard + handler tests for the cockpit endpoint family.
/// The Bridge integration tests skip in CI (Aspire/Docker requirement); these
/// unit tests exercise the same auth assertion the route group uses plus the
/// handler-level happy path.
/// </summary>
public sealed class CockpitEndpointsTests
{
    // ── Route-guard assertion (CockpitPolicy logic) ────────────────────────

    [Theory]
    [InlineData(CockpitPermissions.Roles.Owner, true)]
    [InlineData(CockpitPermissions.Roles.Spouse, true)]
    [InlineData(CockpitPermissions.Roles.Bookkeeper, false)]
    [InlineData(CockpitPermissions.Roles.TaxAdvisor, false)]
    [InlineData(CockpitPermissions.Roles.Contractor, false)]
    [InlineData(CockpitPermissions.Roles.Leaseholder, false)]
    [InlineData(CockpitPermissions.Roles.Prospect, false)]
    [InlineData("unknown-role", false)]
    [InlineData(null, false)]
    public void CockpitPolicy_Admits_Owner_And_Spouse_Only(string? role, bool expected)
    {
        Assert.Equal(expected, CockpitPermissions.CanEnterCockpit(role));
    }

    [Fact]
    public void CockpitPolicy_Is_Registered_With_Authenticated_User_Requirement()
    {
        // Resolves CockpitPolicy from a configured AuthorizationOptions and
        // asserts both the policy registration and that DenyAnonymous is on.
        var options = new AuthorizationOptions();
        options.AddCockpitPolicy();

        var policy = options.GetPolicy(CockpitEndpoints.CockpitPolicyName);
        Assert.NotNull(policy);
        Assert.Contains(policy!.Requirements, r => r is DenyAnonymousAuthorizationRequirement);
        // The role gate is encoded in the assertion requirement; tested above by Theory.
    }

    // ── Handler happy path ──────────────────────────────────────────────────

    [Fact]
    public async Task ListProperties_ReturnsTenantScopedSummaryRows()
    {
        var tenant = new TenantId("tenant-cockpit-test");
        var repo = new InMemoryPropertyRepository();
        await repo.UpsertAsync(NewProperty(tenant, "PROP-001", "123 Main St", "Lehi", "UT"));
        await repo.UpsertAsync(NewProperty(tenant, "PROP-002", "456 Oak Ave", "Provo", "UT"));

        var ctx = new TestTenantContext(tenant.Value);

        var result = await CockpitEndpoints.HandleListPropertiesAsync(ctx, repo, CancellationToken.None);

        Assert.IsType<Ok<PropertySelectorListDto>>(result);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value!.Properties.Count);
        Assert.Contains(result.Value.Properties, p => p.PropertyId == "PROP-001" && p.City == "Lehi" && p.Region == "UT");
        Assert.Contains(result.Value.Properties, p => p.PropertyId == "PROP-002" && p.City == "Provo");
    }

    [Fact]
    public async Task ListProperties_DoesNotLeakOtherTenants()
    {
        var mine  = new TenantId("tenant-mine");
        var other = new TenantId("tenant-other");
        var repo = new InMemoryPropertyRepository();
        await repo.UpsertAsync(NewProperty(mine,  "MINE-1",  "100 My Way",    "Lehi",  "UT"));
        await repo.UpsertAsync(NewProperty(other, "OTHER-1", "999 Their Way", "Provo", "UT"));

        var ctx = new TestTenantContext(mine.Value);
        var result = await CockpitEndpoints.HandleListPropertiesAsync(ctx, repo, CancellationToken.None);

        Assert.NotNull(result.Value);
        Assert.Single(result.Value!.Properties);
        Assert.Equal("MINE-1", result.Value.Properties[0].PropertyId);
    }

    // ── Test fixtures ───────────────────────────────────────────────────────

    private static Property NewProperty(TenantId tenant, string id, string display, string city, string state) =>
        new()
        {
            Id          = new PropertyId(id),
            TenantId    = tenant,
            DisplayName = display,
            Address     = new PostalAddress
            {
                Line1       = display,
                City        = city,
                Region      = state,
                PostalCode  = "00000",
                CountryCode = "US",
            },
            Kind      = PropertyKind.SingleFamily,
            CreatedAt = DateTimeOffset.UnixEpoch,
        };

    private sealed class TestTenantContext : ITenantContext
    {
        public TestTenantContext(string tenantId) => TenantId = tenantId;
        public string TenantId { get; }
        public string UserId => "test-user";
        public IReadOnlyList<string> Roles => new[] { CockpitPermissions.Roles.Owner };
        public bool HasPermission(string permission) => true;
    }
}
