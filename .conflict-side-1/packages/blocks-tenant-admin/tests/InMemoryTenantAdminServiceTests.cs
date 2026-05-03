using Sunfish.Blocks.TenantAdmin.Models;
using Sunfish.Blocks.TenantAdmin.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.TenantAdmin.Tests;

public class InMemoryTenantAdminServiceTests
{
    private static readonly TenantId TestTenant = new("tenant-a");

    [Fact]
    public async Task UpdateTenantProfile_CreatesProfile_WhenNoneExists()
    {
        var svc = new InMemoryTenantAdminService();

        var profile = await svc.UpdateTenantProfileAsync(new UpdateTenantProfileRequest
        {
            TenantId = TestTenant,
            DisplayName = "Acme Corp",
            ContactEmail = "ops@acme.example",
        });

        Assert.Equal("Acme Corp", profile.DisplayName);
        Assert.Equal("ops@acme.example", profile.ContactEmail);

        var roundTripped = await svc.GetTenantProfileAsync(TestTenant);
        Assert.NotNull(roundTripped);
        Assert.Equal("Acme Corp", roundTripped!.DisplayName);
    }

    [Fact]
    public async Task UpdateTenantProfile_PreservesUnchangedFields_OnPartialUpdate()
    {
        var svc = new InMemoryTenantAdminService();

        await svc.UpdateTenantProfileAsync(new UpdateTenantProfileRequest
        {
            TenantId = TestTenant,
            DisplayName = "Acme Corp",
            ContactEmail = "ops@acme.example",
            ContactPhone = "+1-555-0100",
        });

        var updated = await svc.UpdateTenantProfileAsync(new UpdateTenantProfileRequest
        {
            TenantId = TestTenant,
            ContactPhone = "+1-555-0199",
        });

        Assert.Equal("Acme Corp", updated.DisplayName);
        Assert.Equal("ops@acme.example", updated.ContactEmail);
        Assert.Equal("+1-555-0199", updated.ContactPhone);
    }

    [Fact]
    public async Task InviteTenantUser_AssignsIdAndMarksPending()
    {
        var svc = new InMemoryTenantAdminService();

        var user = await svc.InviteTenantUserAsync(new InviteTenantUserRequest
        {
            TenantId = TestTenant,
            Email = "jane@acme.example",
            Role = TenantRole.Admin,
        });

        Assert.False(string.IsNullOrWhiteSpace(user.Id.Value));
        Assert.Equal(TestTenant, user.TenantId);
        Assert.Equal(TenantRole.Admin, user.Role);
        Assert.Null(user.AcceptedAt);

        var listed = await svc.ListTenantUsersAsync(TestTenant);
        Assert.Contains(listed, u => u.Id == user.Id);
    }

    [Fact]
    public async Task AssignRole_UpdatesExistingUser()
    {
        var svc = new InMemoryTenantAdminService();
        var user = await svc.InviteTenantUserAsync(new InviteTenantUserRequest
        {
            TenantId = TestTenant,
            Email = "member@acme.example",
            Role = TenantRole.Member,
        });

        var updated = await svc.AssignRoleAsync(TestTenant, user.Id, TenantRole.Manager);

        Assert.Equal(TenantRole.Manager, updated.Role);
    }

    [Fact]
    public async Task RemoveTenantUser_RemovesRow_AndIsIdempotent()
    {
        var svc = new InMemoryTenantAdminService();
        var user = await svc.InviteTenantUserAsync(new InviteTenantUserRequest
        {
            TenantId = TestTenant,
            Email = "gone@acme.example",
        });

        var firstRemoval = await svc.RemoveTenantUserAsync(TestTenant, user.Id);
        var secondRemoval = await svc.RemoveTenantUserAsync(TestTenant, user.Id);

        Assert.True(firstRemoval);
        Assert.False(secondRemoval);
        var listed = await svc.ListTenantUsersAsync(TestTenant);
        Assert.DoesNotContain(listed, u => u.Id == user.Id);
    }

    [Fact]
    public async Task ActivateBundle_CreatesActiveActivation()
    {
        var svc = new InMemoryTenantAdminService();

        var activation = await svc.ActivateBundleAsync(new ActivateBundleRequest
        {
            TenantId = TestTenant,
            BundleKey = "sunfish.bundles.property-management",
            Edition = "standard",
        });

        Assert.Equal(TestTenant, activation.TenantId);
        Assert.Equal("sunfish.bundles.property-management", activation.BundleKey);
        Assert.Equal("standard", activation.Edition);
        Assert.Null(activation.DeactivatedAt);
    }

    [Fact]
    public async Task ListActiveBundles_FiltersOutDeactivated_AndByTenant()
    {
        var svc = new InMemoryTenantAdminService();

        await svc.ActivateBundleAsync(new ActivateBundleRequest
        {
            TenantId = TestTenant,
            BundleKey = "bundle-one",
            Edition = "standard",
        });

        await svc.ActivateBundleAsync(new ActivateBundleRequest
        {
            TenantId = TestTenant,
            BundleKey = "bundle-two",
            Edition = "pro",
        });

        await svc.ActivateBundleAsync(new ActivateBundleRequest
        {
            TenantId = new TenantId("other-tenant"),
            BundleKey = "bundle-other",
            Edition = "standard",
        });

        await svc.DeactivateBundleAsync(TestTenant, "bundle-one");

        var active = await svc.ListActiveBundlesAsync(TestTenant);

        Assert.Single(active);
        Assert.Equal("bundle-two", active[0].BundleKey);
    }

    [Fact]
    public async Task DeactivateBundle_ReturnsFalse_WhenNoneActive()
    {
        var svc = new InMemoryTenantAdminService();

        var deactivated = await svc.DeactivateBundleAsync(TestTenant, "nonexistent");

        Assert.False(deactivated);
    }

    [Fact]
    public async Task UpdateTenantProfile_ThrowsOnNull_Request()
    {
        var svc = new InMemoryTenantAdminService();
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            svc.UpdateTenantProfileAsync(null!).AsTask());
    }
}
