using System.Text.Json;
using Sunfish.Blocks.Properties.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;
using Xunit;

namespace Sunfish.Blocks.Properties.Tests;

public class PropertyTests
{
    private static Property NewSampleProperty() => new()
    {
        Id = PropertyId.NewId(),
        TenantId = new TenantId("tenant-a"),
        DisplayName = "123 Main St",
        Address = new PostalAddress
        {
            Line1 = "123 Main St",
            City = "Salt Lake City",
            Region = "UT",
            PostalCode = "84101",
            CountryCode = "US",
        },
        Kind = PropertyKind.SingleFamily,
        AcquisitionCost = 425_000m,
        AcquiredAt = new DateTimeOffset(2024, 6, 15, 0, 0, 0, TimeSpan.Zero),
        YearBuilt = 1985,
        TotalSquareFeet = 1850m,
        TotalBedrooms = 3,
        TotalBathrooms = 2.5m,
        CreatedAt = new DateTimeOffset(2026, 4, 28, 0, 0, 0, TimeSpan.Zero),
    };

    [Fact]
    public void Implements_IMustHaveTenant()
    {
        var p = NewSampleProperty();
        Assert.IsAssignableFrom<IMustHaveTenant>(p);
    }

    [Fact]
    public void Json_round_trip_preserves_all_fields()
    {
        var original = NewSampleProperty() with
        {
            ParcelNumber = "27-08-176-001",
            Notes = "Backyard fence needs repair",
            PrimaryPhotoBlobRef = "blob://photos/abc",
            DisposedAt = new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero),
            DisposalReason = "Sold to investor",
        };

        var json = JsonSerializer.Serialize(original);
        var roundTripped = JsonSerializer.Deserialize<Property>(json);

        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void Records_with_same_fields_are_equal()
    {
        var a = NewSampleProperty();
        var b = a with { };

        Assert.Equal(a, b);
    }

    [Fact]
    public void With_expression_changes_field_in_place()
    {
        var a = NewSampleProperty();
        var b = a with { DisplayName = "456 Oak Ave" };

        Assert.NotEqual(a, b);
        Assert.Equal("123 Main St", a.DisplayName);
        Assert.Equal("456 Oak Ave", b.DisplayName);
    }
}
