using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.HttpResults;
using Sunfish.Blocks.Properties.Models;
using Sunfish.Blocks.Properties.Services;
using Sunfish.Bridge.Properties;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Authorization;
using Xunit;

namespace Sunfish.Bridge.Tests.Unit.Properties;

/// <summary>
/// W#74 PR 1 — handler tests for <see cref="PropertiesEndpoints.HandleListPropertiesAsync"/>.
/// Route-guard tests (anonymous → 401) are covered by the AuthenticatedTenantPolicy
/// + standard ASP.NET Core integration tests in the Bridge.Tests.Integration
/// project; these unit tests focus on the handler-level shape + tenant-scoping
/// behavior.
/// </summary>
public sealed class PropertiesEndpointsTests
{
    private static readonly TenantId MyTenant = new("tenant-properties-test");
    private static readonly TenantId OtherTenant = new("tenant-other");

    private sealed class TestTenantContext : ITenantContext
    {
        public TestTenantContext(string tenantId) => TenantId = tenantId;
        public string TenantId { get; }
        public string UserId => "test-user";
        public IReadOnlyList<string> Roles => Array.Empty<string>();
        public bool HasPermission(string permission) => true;
    }

    private static Property MakeProperty(TenantId tenant, string id, string displayName, string? line1, string city, string region, PropertyKind kind = PropertyKind.SingleFamily)
        => new Property
        {
            Id = new PropertyId(id),
            TenantId = tenant,
            DisplayName = displayName,
            Address = new PostalAddress
            {
                Line1 = line1 ?? string.Empty,
                City = city,
                Region = region,
                PostalCode = "00000",
                CountryCode = "US",
            },
            Kind = kind,
            CreatedAt = DateTimeOffset.UnixEpoch,
        };

    private static (IPropertyRepository Repo, ITenantContext Ctx) Build(
        TenantId? callerTenant = null,
        IEnumerable<Property>? seed = null)
    {
        var repo = new InMemoryPropertyRepository();
        if (seed is not null)
        {
            foreach (var p in seed) repo.UpsertAsync(p).GetAwaiter().GetResult();
        }
        var ctx = new TestTenantContext((callerTenant ?? MyTenant).Value);
        return (repo, ctx);
    }

    [Fact]
    public async Task ListProperties_AuthenticatedTenant_ReturnsTenantScopedRows()
    {
        var p1 = MakeProperty(MyTenant, "P1", "123 Main", "123 Main St", "Winchester", "VA");
        var p2 = MakeProperty(MyTenant, "P2", "456 Oak",  "456 Oak St",  "Reston",     "VA");
        var (repo, ctx) = Build(seed: new[] { p1, p2 });

        var result = await PropertiesEndpoints.HandleListPropertiesAsync(ctx, repo, CancellationToken.None);

        Assert.IsType<Ok<PropertyListDto>>(result);
        var dto = ((Ok<PropertyListDto>)result).Value!;
        Assert.Equal(2, dto.Properties.Count);
        Assert.Contains(dto.Properties, p => p.PropertyId == "P1" && p.DisplayName == "123 Main" && p.City == "Winchester" && p.Region == "VA");
        Assert.Contains(dto.Properties, p => p.PropertyId == "P2" && p.DisplayName == "456 Oak"  && p.City == "Reston"     && p.Region == "VA");
    }

    [Fact]
    public async Task ListProperties_WrongTenant_ReturnsEmptyList()
    {
        var theirs = MakeProperty(OtherTenant, "P1", "Theirs", "X", "City", "ST");
        var (repo, ctx) = Build(callerTenant: MyTenant, seed: new[] { theirs });

        var result = await PropertiesEndpoints.HandleListPropertiesAsync(ctx, repo, CancellationToken.None);

        var dto = ((Ok<PropertyListDto>)result).Value!;
        Assert.Empty(dto.Properties);
    }

    [Fact]
    public async Task ListProperties_EmptyTenant_ReturnsEmptyArrayNotNull()
    {
        var (repo, ctx) = Build(seed: Array.Empty<Property>());

        var result = await PropertiesEndpoints.HandleListPropertiesAsync(ctx, repo, CancellationToken.None);

        var dto = ((Ok<PropertyListDto>)result).Value!;
        Assert.NotNull(dto.Properties);
        Assert.Empty(dto.Properties);
    }

    [Fact]
    public async Task ListProperties_PropertyWithoutAddressLine_ReturnsEmptyStringField()
    {
        var p = MakeProperty(MyTenant, "P-no-line", "No Address", null, "City", "ST");
        var (repo, ctx) = Build(seed: new[] { p });

        var result = await PropertiesEndpoints.HandleListPropertiesAsync(ctx, repo, CancellationToken.None);

        var dto = ((Ok<PropertyListDto>)result).Value!;
        var item = dto.Properties.Single();
        // PostalAddress.Line1 is required; null falls through to empty string at the DTO level.
        Assert.Equal(string.Empty, item.AddressLine1);
    }

    [Fact]
    public async Task ListProperties_DefaultsStatusToActive_UntilPropertyStatusFieldShips()
    {
        var p = MakeProperty(MyTenant, "P1", "Test", "X", "City", "ST");
        var (repo, ctx) = Build(seed: new[] { p });

        var result = await PropertiesEndpoints.HandleListPropertiesAsync(ctx, repo, CancellationToken.None);
        var dto = ((Ok<PropertyListDto>)result).Value!;
        // Per hand-off §3.1 step 4 — default "Active" until Property.Status field lands.
        Assert.Equal("Active", dto.Properties.Single().Status);
    }

    [Fact]
    public async Task ListProperties_DefaultsUnitCountToZero_UntilPropertyUnitsWired()
    {
        var p = MakeProperty(MyTenant, "P1", "Test", "X", "City", "ST");
        var (repo, ctx) = Build(seed: new[] { p });

        var result = await PropertiesEndpoints.HandleListPropertiesAsync(ctx, repo, CancellationToken.None);
        var dto = ((Ok<PropertyListDto>)result).Value!;
        // Per hand-off §3.1 step 4 — PropertyUnit child ships in a follow-on; default 0.
        Assert.Equal(0, dto.Properties.Single().UnitCount);
    }

    [Fact]
    public async Task ListProperties_EntityTagEcho_IsNullUntilW64Ships()
    {
        var p = MakeProperty(MyTenant, "P1", "Test", "X", "City", "ST");
        var (repo, ctx) = Build(seed: new[] { p });

        var result = await PropertiesEndpoints.HandleListPropertiesAsync(ctx, repo, CancellationToken.None);
        var dto = ((Ok<PropertyListDto>)result).Value!;
        // Per hand-off §3.1 step 5 — EntityTag is null until W#64 ships Property.EntityTag.
        Assert.Null(dto.Properties.Single().EntityTag);
    }

    [Fact]
    public async Task ListProperties_KindEnum_RendersAsString()
    {
        var p = MakeProperty(MyTenant, "P1", "Test", "X", "City", "ST", kind: PropertyKind.MultiUnit);
        var (repo, ctx) = Build(seed: new[] { p });

        var result = await PropertiesEndpoints.HandleListPropertiesAsync(ctx, repo, CancellationToken.None);
        var dto = ((Ok<PropertyListDto>)result).Value!;
        Assert.Equal("MultiUnit", dto.Properties.Single().Kind);
    }
}
