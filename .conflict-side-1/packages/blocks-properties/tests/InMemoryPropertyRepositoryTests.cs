using Sunfish.Blocks.Properties.Models;
using Sunfish.Blocks.Properties.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.Properties.Tests;

public class InMemoryPropertyRepositoryTests
{
    private static readonly TenantId TenantA = new("tenant-a");
    private static readonly TenantId TenantB = new("tenant-b");

    private static Property NewProperty(TenantId tenant, string displayName, PropertyKind kind = PropertyKind.SingleFamily) => new()
    {
        Id = PropertyId.NewId(),
        TenantId = tenant,
        DisplayName = displayName,
        Address = new PostalAddress
        {
            Line1 = displayName,
            City = "Salt Lake City",
            Region = "UT",
            PostalCode = "84101",
            CountryCode = "US",
        },
        Kind = kind,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task Upsert_then_GetByIdAsync_round_trips()
    {
        var repo = new InMemoryPropertyRepository();
        var property = NewProperty(TenantA, "123 Main St");

        await repo.UpsertAsync(property);
        var fetched = await repo.GetByIdAsync(TenantA, property.Id);

        Assert.Equal(property, fetched);
    }

    [Fact]
    public async Task GetByIdAsync_returns_null_for_unknown_id()
    {
        var repo = new InMemoryPropertyRepository();
        var fetched = await repo.GetByIdAsync(TenantA, PropertyId.NewId());
        Assert.Null(fetched);
    }

    [Fact]
    public async Task GetByIdAsync_isolates_tenants()
    {
        var repo = new InMemoryPropertyRepository();
        var property = NewProperty(TenantA, "123 Main St");
        await repo.UpsertAsync(property);

        var fromOtherTenant = await repo.GetByIdAsync(TenantB, property.Id);

        Assert.Null(fromOtherTenant);
    }

    [Fact]
    public async Task ListByTenantAsync_returns_only_owning_tenants_records()
    {
        var repo = new InMemoryPropertyRepository();
        await repo.UpsertAsync(NewProperty(TenantA, "Tenant-A House 1"));
        await repo.UpsertAsync(NewProperty(TenantA, "Tenant-A House 2"));
        await repo.UpsertAsync(NewProperty(TenantB, "Tenant-B House"));

        var aProperties = await repo.ListByTenantAsync(TenantA);
        var bProperties = await repo.ListByTenantAsync(TenantB);

        Assert.Equal(2, aProperties.Count);
        Assert.Single(bProperties);
        Assert.All(aProperties, p => Assert.Equal(TenantA, p.TenantId));
        Assert.All(bProperties, p => Assert.Equal(TenantB, p.TenantId));
    }

    [Fact]
    public async Task ListByTenantAsync_excludes_disposed_records_by_default()
    {
        var repo = new InMemoryPropertyRepository();
        var live = NewProperty(TenantA, "Live");
        var disposed = NewProperty(TenantA, "Disposed");
        await repo.UpsertAsync(live);
        await repo.UpsertAsync(disposed);
        await repo.SoftDeleteAsync(TenantA, disposed.Id, "sold", DateTimeOffset.UtcNow);

        var defaults = await repo.ListByTenantAsync(TenantA);
        Assert.Single(defaults);
        Assert.Equal(live.Id, defaults[0].Id);
    }

    [Fact]
    public async Task ListByTenantAsync_includes_disposed_records_when_requested()
    {
        var repo = new InMemoryPropertyRepository();
        var live = NewProperty(TenantA, "Live");
        var disposed = NewProperty(TenantA, "Disposed");
        await repo.UpsertAsync(live);
        await repo.UpsertAsync(disposed);
        await repo.SoftDeleteAsync(TenantA, disposed.Id, "sold", DateTimeOffset.UtcNow);

        var all = await repo.ListByTenantAsync(TenantA, includeDisposed: true);

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task SoftDeleteAsync_stamps_DisposedAt_and_DisposalReason()
    {
        var repo = new InMemoryPropertyRepository();
        var property = NewProperty(TenantA, "123 Main St");
        await repo.UpsertAsync(property);
        var disposalTime = new DateTimeOffset(2026, 4, 28, 12, 0, 0, TimeSpan.Zero);

        await repo.SoftDeleteAsync(TenantA, property.Id, "sold to investor", disposalTime);

        var fetched = await repo.GetByIdAsync(TenantA, property.Id);
        Assert.NotNull(fetched);
        Assert.Equal(disposalTime, fetched!.DisposedAt);
        Assert.Equal("sold to investor", fetched.DisposalReason);
    }

    [Fact]
    public async Task SoftDeleteAsync_is_a_no_op_for_unknown_id()
    {
        var repo = new InMemoryPropertyRepository();
        // Should not throw.
        await repo.SoftDeleteAsync(TenantA, PropertyId.NewId(), "sold", DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task SoftDeleteAsync_does_not_cross_tenants()
    {
        var repo = new InMemoryPropertyRepository();
        var property = NewProperty(TenantA, "123 Main St");
        await repo.UpsertAsync(property);

        await repo.SoftDeleteAsync(TenantB, property.Id, "wrong tenant", DateTimeOffset.UtcNow);

        var fetched = await repo.GetByIdAsync(TenantA, property.Id);
        Assert.NotNull(fetched);
        Assert.Null(fetched!.DisposedAt);
    }

    [Fact]
    public async Task SoftDeleteAsync_throws_on_blank_reason()
    {
        var repo = new InMemoryPropertyRepository();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            repo.SoftDeleteAsync(TenantA, PropertyId.NewId(), "  ", DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task UpsertAsync_overwrites_prior_record_with_same_key()
    {
        var repo = new InMemoryPropertyRepository();
        var v1 = NewProperty(TenantA, "Original Name");
        await repo.UpsertAsync(v1);

        var v2 = v1 with { DisplayName = "Updated Name" };
        await repo.UpsertAsync(v2);

        var fetched = await repo.GetByIdAsync(TenantA, v1.Id);
        Assert.Equal("Updated Name", fetched!.DisplayName);
    }
}
